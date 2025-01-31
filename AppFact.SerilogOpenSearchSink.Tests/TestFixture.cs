using System.Text.Json.Nodes;
using AppFact.SerilogOpenSearchSink.Serialization;
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
        Util.GetSolutionDirectory();
        Task.Run(InitializeAsync).Wait();
    }

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder()
            .WithImage("opensearchproject/opensearch")
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("OPENSEARCH_INITIAL_ADMIN_PASSWORD", "Kec4##Dd2") // annoying password requirements
            .WithPortBinding(9200, true)
            .WithAutoRemove(true)
            .Build();
        await _container.StartAsync();
        for (var i = 0;; i++)
        {
            if (i > 300) throw new Exception("OpenSearch container did not start in time");
            try
            {
                var client = new OpenSearchClient(GetConnectionSettings());
                var pingResponse = await client.PingAsync();
                if (pingResponse.IsValid)
                    break;
                await Task.Delay(100);
            }
            catch (Exception)
            {
                await Task.Delay(100);
            }
        }
    }

    private ConnectionSettings GetConnectionSettings()
    {
        var uri = new Uri("https://" + _container.Hostname + ":" + _container.GetMappedPublicPort(9200));
        var pool = new SingleNodeConnectionPool(uri);
        var connSettings =
            new ConnectionSettings(
                pool,
                OpenSearchSerializer.SourceSerializerFactory
            );
        connSettings.ServerCertificateValidationCallback((_, _, _, _) => true);
        connSettings.BasicAuthentication("admin", "Kec4##Dd2");
        return connSettings;
    }

    public (Logger, OpenSearchSink) GetLogger(OpenSearchSinkOptions? opts = default)
    {
        var connSettings = GetConnectionSettings();
        connSettings.DefaultIndex(Guid.NewGuid().ToString());
        opts ??= new OpenSearchSinkOptions
        {
            Tick = TimeSpan.FromMilliseconds(1),
        };
        var sink = new OpenSearchSink(connSettings, opts);
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        return (logger, sink);
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