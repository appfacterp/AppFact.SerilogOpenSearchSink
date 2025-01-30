using System.Collections.Concurrent;
using OpenSearch.Client;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace AppFact.SerilogOpenSearchSink;

/// <summary>
/// Implementation of <see cref="ILogEventSink"/> that sends log events to an OpenSearch cluster.
/// </summary>
public class OpenSearchSink : ILogEventSink, IDisposable
{
    internal readonly OpenSearchClient Client;
    private readonly BlockingCollection<LogEvent> _queue;
    private readonly int? _maxBatchSize;
    private readonly TimeSpan _tick;
    private readonly LogEventMapper _mapper;
    private bool _processExitRegistered = false;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _daemon;

    /// <summary>
    /// Mapper used to transform a <see cref="LogEvent"/> into an object that will be sent to OpenSearch.
    /// </summary>
    public delegate IRecoverable LogEventMapper(LogEvent logEvent);

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="settings">Connection Settings</param>
    /// <param name="options">Settings for how logs are sent</param>
    /// <exception cref="Exception">If connecting to cluster fails</exception>
    public OpenSearchSink(IConnectionSettingsValues settings, OpenSearchSinkOptions options = default!)
    {
        // init parameters
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        options ??= new OpenSearchSinkOptions();
        _maxBatchSize = options.MaxBatchSize;
        _tick = options.Tick;
        _mapper = options.Mapper ?? AppFactSerilogOpenSearchEvent.MapEvent;
        _queue = options.QueueSizeLimit is not null
            ? new(new ConcurrentQueue<LogEvent>(), options.QueueSizeLimit.Value)
            : new(new ConcurrentQueue<LogEvent>());

        // init opensearch client and check connection
        Client = new OpenSearchClient(settings);
        var pingResponse = Client.Ping();
        if (!pingResponse.IsValid)
        {
            if (!options.SuppressThrowOnFailedInit)
                throw new Exception("Unable to connect to opensearch cluster", pingResponse.OriginalException);
            SelfLog.WriteLine("Unable to connect to opensearch cluster: {0}", pingResponse.OriginalException.Message);
        }

        // start sending logs to OpenSearch in background
        _daemon = Task.Run(Daemon);
        // register process exit handler to send remaining logs before exiting
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    /// <inherit/>
    public void Emit(LogEvent logEvent)
    {
        if (_processExitRegistered)
        {
            SelfLog.WriteLine("Process exit registered, no more events will be accepted");
            return;
        }

        if (!_queue.TryAdd(logEvent))
        {
            SelfLog.WriteLine("Queue is full, dropping log event");
        }
    }

    /// <summary>
    /// Task that runs in the background and sends log events to opensearch
    /// </summary>
    private async Task Daemon()
    {
        while (true)
        {
            // use stop as copy of the current value of _processExitRegistered to avoid race conditions
            var stop = _processExitRegistered;
            // considering, that PeriodicTimer is only available in >= .NET 6, a manual approach to timing is used
            var start = DateTime.UtcNow;


            // aggregate as many events as possible while respecting the max batch size
            var events = new List<LogEvent>();
            for (var i = 0; (_maxBatchSize is null || i < _maxBatchSize) && _queue.TryTake(out var logEvent); i++)
            {
                events.Add(logEvent);
            }

            // send logs to OpenSearch if there are any
            if (events.Count > 0)
            {
                var success = await SendBulkEventsAsync(events)
                    .ConfigureAwait(false);

                // re-emit log events on failure 
                if (!success)
                {
                    foreach (var logEvent in events)
                    {
                        Emit(logEvent);
                    }
                }

                // the count of events being the batch size indicates that there might be more events in the queue,
                // in that case, skip the delay and continue sending the next batch
                if (events.Count == _maxBatchSize)
                    continue;
            }

            if (stop)
                break;

            // wait for the next tick
            var end = DateTime.UtcNow;
            var diff = end - start;
            if (diff >= _tick) continue;
            try
            {
                // task will be cancelled when the process exit is requested
                // i.e. stop waiting for the next tick and send the remaining logs
                await Task.Delay(_tick - diff, _cancellationTokenSource.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }
    }

    private async Task<bool> SendBulkEventsAsync(List<LogEvent> events)
    {
        List<IRecoverable> mapped;
        try
        {
            mapped = events.Select(e => _mapper(e)).ToList();
        }
        catch (Exception e)
        {
            SelfLog.WriteLine("failed to map events: {0}", e.Message);
            return false;
        }

        return await SendBulkAsync(mapped, false)
            .ConfigureAwait(false);
    }

    private async Task<bool> SendBulkAsync(List<IRecoverable> events, bool isRecovering)
    {
        try
        {
            // map events to custom format (default is the private Event class below, mapped via the MapEvent delegate)
            // so that OpenSearch can index them
            var result = await Client.IndexManyAsync(events)
                .ConfigureAwait(false);
            // log errors if any
            if (result.Errors || !result.IsValid)
            {
                throw new Exception("failed to index events into OpenSearch: " + result.DebugInformation,
                    result.OriginalException);
            }
        }
        catch (Exception e)
        {
            if (isRecovering)
            {
                Console.WriteLine(e);
                return false;
            }


            // try to recover the events and send them again
            var recovered = Recover(events);
            if (recovered is null)
            {
                Console.WriteLine(e);
                return false;
            }

            return await SendBulkAsync(recovered, true)
                .ConfigureAwait(false);
        }

        return true;
    }

    private List<IRecoverable>? Recover(List<IRecoverable> events)
    {
        var recovered = new List<IRecoverable>(events.Count);

        foreach (var recoverable in events)
        {
            var recoveredValue = recoverable.RecoverSafe(Client.SourceSerializer);
            if (recoveredValue is not null)
                recovered.Add(recoveredValue);
            else
                SelfLog.WriteLine("Failed to recover object. dropping");
        }

        return recovered.Count == 0 ? null : recovered;
    }

    internal void OnProcessExit(object sender, EventArgs args)
    {
        _processExitRegistered = true;
        _cancellationTokenSource.Cancel();
        _daemon.Wait();
    }

    private bool _disposed;

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;
        // wait for last logs to be sent off
        // before destruction
        OnProcessExit(null!, null!);
        _queue.Dispose();
        if (disposing)
            GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
    }

    /// <summary>
    /// Disposes of resources
    /// </summary>
    ~OpenSearchSink()
    {
        Dispose(false);
    }
}