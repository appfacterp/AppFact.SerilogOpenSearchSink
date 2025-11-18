namespace AppFact.SerilogOpenSearchSink;

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
    /// should the sink suppress throwing an exception if the ping to the OpenSearch cluster fails on startup. this has no effect if the ping fails after the sink has started
    /// </summary>
    public bool SuppressThrowOnFailedInit { get; set; } = false;

    /// <summary>
    /// Factory function to create the index name for each batch. If not set, the default index configured in the ConnectionSettings will be used.
    /// This can be used to append the date to the index name, e.g. "logs-2024-01-01".
    /// </summary>
    public Func<string>? IndexNameFactory { get; set; }
}