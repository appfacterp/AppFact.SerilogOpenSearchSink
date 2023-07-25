using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using OpenSearch.Client;
using OpenSearch.Net;
using Serilog;
using Serilog.Core;

namespace AppFact.SerilogOpenSearchSink.Tests;

public class TestFixture : IAsyncDisposable
{
    private IContainer _container = null!;

    public TestFixture()
    {
        Task.Run(InitializeAsync).Wait();
    }

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder()
            .WithImage("opensearchproject/opensearch")
            .WithEnvironment("discovery.type", "single-node")
            .WithPortBinding(9200, true)
            .WithAutoRemove(true)
            .Build();
        await _container.StartAsync();
        for (var i = 0;; i++)
        {
            if (i > 30) throw new Exception("OpenSearch container did not start in time");
            try
            {
                var client = new OpenSearchClient(GetConnectionSettings());
                var pingResponse = await client.PingAsync();
                if (pingResponse.IsValid)
                    break;
                await Task.Delay(1000);
            }
            catch (Exception)
            {
                await Task.Delay(1000);
            }
        }
    }

    private ConnectionSettings GetConnectionSettings()
    {
        var connSettings =
            new ConnectionSettings(
                new Uri("https://" + _container.Hostname + ":" + _container.GetMappedPublicPort(9200)));
        connSettings.ServerCertificateValidationCallback((_, _, _, _) => true);
        connSettings.BasicAuthentication("admin", "admin");
        return connSettings;
    }

    public (Logger, OpenSearchClient) GetLogger(OpenSearchSinkOptions? opts = default)
    {
        var connSettings = GetConnectionSettings();
        connSettings.DefaultIndex(Guid.NewGuid().ToString());
        opts ??= new OpenSearchSinkOptions
        {
            Tick = TimeSpan.FromMilliseconds(1),
            MaxBatchSize = null
        };
        var sink = new OpenSearchSink(connSettings, opts);
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        return (logger, sink.Client);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}

public class TestBase : IClassFixture<TestFixture>
{
    public TestFixture F { get; }

    public TestBase(TestFixture f)
    {
        F = f;
    }
}