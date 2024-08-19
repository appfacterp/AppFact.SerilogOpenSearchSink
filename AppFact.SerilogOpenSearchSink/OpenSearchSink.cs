using System.Collections.Concurrent;
using OpenSearch.Client;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace AppFact.SerilogOpenSearchSink;

/// <summary>
/// Implementation of <see cref="ILogEventSink"/> that sends log events to an OpenSearch cluster.
/// </summary>
public class OpenSearchSink : ILogEventSink
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
    public delegate object LogEventMapper(LogEvent logEvent);

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
        _mapper = options.Mapper ?? MapEvent;
        _queue = options.QueueSizeLimit is not null
            ? new(new ConcurrentQueue<LogEvent>(), options.QueueSizeLimit.Value)
            : new(new ConcurrentQueue<LogEvent>());

        // init opensearch client and check connection
        Client = new OpenSearchClient(settings);
        var pingResponse = Client.Ping();
        if (!pingResponse.IsValid)
        {
            if (options.ThrowOnFailedPing)
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

        _queue.TryAdd(logEvent);
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
                await SendBulkAsync(events)
                    .ConfigureAwait(false);
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

    private async Task SendBulkAsync(IEnumerable<LogEvent> events)
    {
        try
        {
            // map events to custom format (default is the private Event class below, mapped via the MapEvent delegate)
            // so that OpenSearch can index them
            var result = await Client.IndexManyAsync(
                    events.Select(e => _mapper(e)).ToList())
                .ConfigureAwait(false);
            // log errors if any
            if (result.Errors)
            {
                SelfLog.WriteLine("failed to index events into OpenSearch: {0}", result.DebugInformation);
            }
        }
        catch (Exception e)
        {
            SelfLog.WriteLine("failed to index events into OpenSearch: {0}", e.Message);
        }
    }

    private static object MapEvent(LogEvent e)
    {
        var message = e.RenderMessage();
        var props = e.Properties
            .Where(p => p.Key != "EventId" && (p.Value is not ScalarValue { Value: null }))
            .ToDictionary(k => k.Key, v => v.Value switch
            {
                ScalarValue scalar => scalar.Value!,
                var value => value
            });

        return new Event
        {
            Timestamp = e.Timestamp,
            Level = e.Level.ToString(),
            Message = message,
            Props = props,
            Template = e.MessageTemplate.Text,
            Exception = e.Exception
        };
    }

    internal class Event
    {
        public required DateTimeOffset Timestamp { get; init; }
        public required string Level { get; init; }
        public required string Message { get; init; }
        public required string Template { get; init; }
        public required IDictionary<string, object> Props { get; init; }
        public required Exception? Exception { get; init; }
    }

    internal void OnProcessExit(object sender, EventArgs args)
    {
        _processExitRegistered = true;
        _cancellationTokenSource.Cancel();
        _daemon.Wait();
    }
}

/// <summary>
/// Options to configure how logs are sent to OpenSearch
/// </summary>
public class OpenSearchSinkOptions
{
    /// <summary>
    /// the maximum number of logs to send to OpenSearch in one request
    /// </summary>
    public int? MaxBatchSize { get; init; } = null;

    /// <summary>
    /// how often to send logs to OpenSearch
    /// </summary>
    public TimeSpan Tick { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// what format logs are converted to before being sent to OpenSearch
    /// </summary>
    public OpenSearchSink.LogEventMapper? Mapper { get; init; } = null;

    /// <summary>
    /// the maximum number of logs to queue before dropping new logs
    /// </summary>
    public int? QueueSizeLimit { get; init; }

    /// <summary>
    /// should the sink throw an exception if the ping to the OpenSearch cluster fails on startup this has no effect if the ping fails after the sink has started
    /// </summary>
    public bool ThrowOnFailedPing { get; set; }
}