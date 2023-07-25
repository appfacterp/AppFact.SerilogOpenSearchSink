using OpenSearch.Client;
using Serilog;
using Serilog.Configuration;

namespace AppFact.SerilogOpenSearchSink;

/// <summary>
/// Handy Serilog extensions
/// </summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Adds the OpenSearch sink to the logger configuration using the provided connection settings and options
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="settings">connection settings</param>
    /// <param name="options">options for how logs are sent to OpenSearch</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">when <paramref name="configuration"/> is null</exception>
    public static LoggerConfiguration OpenSearch(this LoggerSinkConfiguration configuration,
        IConnectionSettingsValues settings,
        OpenSearchSinkOptions options = default!)
    {
        _ = configuration ?? throw new ArgumentNullException(nameof(configuration));
        return configuration.Sink(new OpenSearchSink(settings, options));
    }

    /// <summary>
    /// Adds the OpenSearch sink to the logger configuration using the provided parameters
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="uri">uri of the OpenSearch cluster</param>
    /// <param name="basicAuthUser">username for basic auth</param>
    /// <param name="basicAuthPassword">password for basic auth</param>
    /// <param name="index">index where logs are sent to</param>
    /// <param name="maxBatchSize">how many logs (maximum) should be sent to OpenSearch in one single request/on each tick</param>
    /// <param name="tickInSeconds">how often logs are taken from the internal queue and sent to OpenSearch</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">when required parameters are null. see null annotations</exception>
    public static LoggerConfiguration OpenSearch(this LoggerSinkConfiguration configuration,
        string uri,
        string basicAuthUser, string basicAuthPassword, string index = "logs", int? maxBatchSize = 1000,
        double tickInSeconds = 1.0)
    {
        _ = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _ = uri ?? throw new ArgumentNullException(nameof(uri));
        _ = basicAuthUser ?? throw new ArgumentNullException(nameof(basicAuthUser));
        _ = basicAuthPassword ?? throw new ArgumentNullException(nameof(basicAuthPassword));
        _ = index ?? throw new ArgumentNullException(nameof(index));

        var conn = new ConnectionSettings(new Uri(uri));
        conn.BasicAuthentication(basicAuthUser, basicAuthPassword);
        conn.DefaultIndex(index);

        var opts = new OpenSearchSinkOptions()
        {
            MaxBatchSize = maxBatchSize,
            Tick = TimeSpan.FromSeconds(tickInSeconds),
        };

        return configuration.OpenSearch(conn, opts);
    }
}