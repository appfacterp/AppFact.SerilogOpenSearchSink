using OpenSearch.Client;
using OpenSearch.Net;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

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
    /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink. Ignored when levelSwitch is specified.</param>
    /// <param name="levelSwitch">A switch allowing the pass-through minimum level to be changed at runtime.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">when <paramref name="configuration"/> is null</exception>
    public static LoggerConfiguration OpenSearch(this LoggerSinkConfiguration configuration,
        IConnectionSettingsValues settings,
        OpenSearchSinkOptions options = default!,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch? levelSwitch = null)
    {
        _ = configuration ?? throw new ArgumentNullException(nameof(configuration));
        return configuration.Sink(new OpenSearchSink(settings, options), restrictedToMinimumLevel, levelSwitch);
    }

    /// <summary>
    /// Adds the OpenSearch sink to the logger configuration using the provided parameters
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="uri">Uri of the OpenSearch cluster</param>
    /// <param name="basicAuthUser">username for basic auth</param>
    /// <param name="basicAuthPassword">password for basic auth</param>
    /// <param name="index">index where logs are sent to</param>
    /// <param name="maxBatchSize">how many logs (maximum) should be sent to OpenSearch in one single request/on each tick</param>
    /// <param name="tickInSeconds">how often logs are taken from the internal queue and sent to OpenSearch</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink. Ignored when levelSwitch is specified.</param>
    /// <param name="levelSwitch">A switch allowing the pass-through minimum level to be changed at runtime.</param>
    /// <param name="bypassSsl">Bypass OpenSearch node SSL certificate if it's untrusted. Default behaviour for .NET is to throw exception in such cases.</param>
    /// <param name="suppressThrowOnFailedInit">should the sink suppress throwing an exception if the ping to the OpenSearch cluster fails on startup. this has no effect if the ping fails after the sink has started</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">when required parameters are null. see null annotations</exception>
    public static LoggerConfiguration OpenSearch(this LoggerSinkConfiguration configuration,
        string uri,
        string basicAuthUser,
        string basicAuthPassword,
        string index = "logs",
        int? maxBatchSize = 1000,
        double tickInSeconds = 1.0,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch? levelSwitch = null,
        bool bypassSsl = false,
        bool suppressThrowOnFailedInit = false)
    {
        return configuration.OpenSearch(new[] { new Uri(uri) },
            basicAuthUser,
            basicAuthPassword,
            index,
            maxBatchSize,
            tickInSeconds,
            restrictedToMinimumLevel,
            levelSwitch,
            bypassSsl,
            suppressThrowOnFailedInit);
    }


    /// <summary>
    /// Adds the OpenSearch sink to the logger configuration using the provided parameters
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="connectionStrings">Will use <see cref="SniffingConnectionPool"/> if several urls are provided and <see cref="SingleNodeConnectionPool "/> if a single one is provided</param>
    /// <param name="basicAuthUser">username for basic auth</param>
    /// <param name="basicAuthPassword">password for basic auth</param>
    /// <param name="index">index where logs are sent to</param>
    /// <param name="maxBatchSize">how many logs (maximum) should be sent to OpenSearch in one single request/on each tick</param>
    /// <param name="tickInSeconds">how often logs are taken from the internal queue and sent to OpenSearch</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink. Ignored when levelSwitch is specified.</param>
    /// <param name="levelSwitch">A switch allowing the pass-through minimum level to be changed at runtime.</param>
    /// <param name="bypassSsl">Bypass OpenSearch node SSL certificate if it's untrusted. Default behaviour for .NET is to throw exception in such cases.</param>
    /// <param name="suppressThrowOnFailedInit">should the sink suppress throwing an exception if the ping to the OpenSearch cluster fails on startup. this has no effect if the ping fails after the sink has started</param>
    /// <returns></returns>
    static public LoggerConfiguration OpenSearch(this LoggerSinkConfiguration configuration,
        ICollection<Uri> connectionStrings,
        string basicAuthUser,
        string basicAuthPassword,
        string index = "logs",
        int? maxBatchSize = 1000,
        double tickInSeconds = 1.0,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch? levelSwitch = null,
        bool bypassSsl = false,
        bool suppressThrowOnFailedInit = false)
    {
        _ = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _ = connectionStrings ?? throw new ArgumentNullException(nameof(connectionStrings));
        _ = basicAuthUser ?? throw new ArgumentNullException(nameof(basicAuthUser));
        _ = basicAuthPassword ?? throw new ArgumentNullException(nameof(basicAuthPassword));
        _ = index ?? throw new ArgumentNullException(nameof(index));

        if (!connectionStrings.Any())
        {
            throw new ArgumentException("Provide at least one connection string");
        }

        IConnectionPool? connectionPool = null;
        if (connectionStrings.Count() > 1)
        {
            connectionPool = new SniffingConnectionPool(connectionStrings);
        }
        else
        {
            connectionPool = new SingleNodeConnectionPool(connectionStrings.Single());
        }

        var conn = new ConnectionSettings(connectionPool,
            sourceSerializer: OpenSearchSerializer.SourceSerializerFactory);

        conn.BasicAuthentication(basicAuthUser, basicAuthPassword);
        conn.DefaultIndex(index);

        if (bypassSsl)
        {
            conn.ServerCertificateValidationCallback(CertificateValidations.AllowAll);
        }

        var opts = new OpenSearchSinkOptions()
        {
            MaxBatchSize = maxBatchSize,
            Tick = TimeSpan.FromSeconds(tickInSeconds),
            SuppressThrowOnFailedInit = suppressThrowOnFailedInit
        };

        return configuration.OpenSearch(conn, opts, restrictedToMinimumLevel, levelSwitch);
    }
}