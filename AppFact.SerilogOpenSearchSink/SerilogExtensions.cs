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
}