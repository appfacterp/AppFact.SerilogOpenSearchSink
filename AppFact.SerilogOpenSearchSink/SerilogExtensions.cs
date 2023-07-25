using OpenSearch.Client;
using Serilog;
using Serilog.Configuration;

namespace AppFact.SerilogOpenSearchSink;

public static class SerilogExtensions
{
    public static LoggerConfiguration OpenSearch(this LoggerSinkConfiguration configuration,
        IConnectionSettingsValues settings,
        OpenSearchSinkOptions options = default!)
    {
        return configuration.Sink(new OpenSearchSink(settings, options));
    }

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