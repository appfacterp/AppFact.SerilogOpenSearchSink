using System.Collections.Concurrent;
using OpenSearch.Client;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace AppFact.SerilogOpenSearchSink;

public class OpenSearchSink : ILogEventSink
{
    private readonly OpenSearchClient _client;
    private readonly ConcurrentQueue<LogEvent> _queue = new();
    private readonly int? _maxBatchSize;
    private readonly TimeSpan _tick;
    private readonly LogEventMapper _mapper;
    private bool _processExitRegistered = false;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _daemon;

    public delegate object LogEventMapper(LogEvent logEvent);

    public OpenSearchSink(IConnectionSettingsValues settings, OpenSearchSinkOptions options = default!)
    {
        // init parameters
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        options ??= new OpenSearchSinkOptions();
        _maxBatchSize = options.MaxBatchSize;
        _tick = options.Tick;
        _mapper = options.Mapper ?? MapEvent;

        // init opensearch client and check connection
        _client = new OpenSearchClient(settings);
        var pingResponse = _client.Ping();
        if (!pingResponse.IsValid)
        {
            throw new Exception("Unable to connect to opensearch cluster", pingResponse.OriginalException);
        }

        // start sending logs to opensearch in background
        _daemon = Daemon();
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

        // TODO: consider whether the queue should be bounded, i.e. if the queue is full, drop the event? That is how the grafana loki sink is implemented.
        _queue.Enqueue(logEvent);
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
            for (var i = 0; (_maxBatchSize is null || i < _maxBatchSize) && _queue.TryDequeue(out var logEvent); i++)
            {
                events.Add(logEvent);
            }

            // send logs to opensearch if there are any
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
            // so that opensearch can index them
            var result = await _client.IndexManyAsync(
                    events.Select(e => _mapper(e)).ToList())
                .ConfigureAwait(false);
            // log errors if any
            if (result.Errors)
            {
                SelfLog.WriteLine("failed to index events into opensearch: {0}", result.DebugInformation);
            }
        }
        catch (Exception e)
        {
            SelfLog.WriteLine("failed to index events into opensearch: {0}", e);
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

    private class Event
    {
        public required DateTimeOffset Timestamp { get; init; }
        public required string Level { get; init; }
        public required string Message { get; init; }
        public required string Template { get; init; }
        public required IDictionary<string, object> Props { get; init; }
        public required Exception? Exception { get; init; }
    }

    private void OnProcessExit(object sender, EventArgs args)
    {
        _processExitRegistered = true;
        _cancellationTokenSource.Cancel();
        _daemon.Wait();
    }
}

public class OpenSearchSinkOptions
{
    /// <summary>
    /// the maximum number of logs to send to opensearch in one request
    /// </summary>
    public int? MaxBatchSize { get; init; } = null;

    /// <summary>
    /// how often to send logs to opensearch
    /// </summary>
    public TimeSpan Tick { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// what format logs are converted to before being sent to opensearch
    /// </summary>
    public OpenSearchSink.LogEventMapper? Mapper { get; init; } = null;
}