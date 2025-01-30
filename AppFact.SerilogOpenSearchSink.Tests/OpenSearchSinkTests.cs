using FluentAssertions;

namespace AppFact.SerilogOpenSearchSink.Tests;

public class OpenSearchSinkTests : TestBase
{
    public OpenSearchSinkTests(TestFixture f) : base(f)
    {
    }

    [Fact]
    public async Task SendsLogsToOpenSearch()
    {
        // Arrange
        var (logger, sink) = F.GetLogger();
        var message = Guid.NewGuid().ToString();

        // Act
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        logger.Information(message);

        // Assert
        await Task.Delay(2000);
        var result = await sink.Client.SearchAsync<AppFactSerilogOpenSearchEvent>(s => s.Query(q => q.MatchAll()));
        result.Total.Should().Be(1);
        result.Documents.First().Message.Should().Be(message);
    }

    [Fact]
    public async Task SendsLogsInDefinedFormat()
    {
        // Arrange
        var (logger, sink) = F.GetLogger(new OpenSearchSinkOptions()
        {
            Tick = TimeSpan.FromMilliseconds(1),
            Mapper = l => new TestEvent
            {
                Message = l.RenderMessage() + " test234",
                Abc = "abc"
            }
        });

        // Act
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        logger.Information("test");

        // Assert
        await Task.Delay(2000);
        var result = await sink.Client.SearchAsync<TestEvent>(s => s.Query(q => q.MatchAll()));
        result.Total.Should().Be(1);
        result.Documents.First().Message.Should().Be("test test234");
        result.Documents.First().Abc.Should().Be("abc");
    }

    private class TestEvent
    {
        public required string Message { get; init; }
        public required string Abc { get; init; }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    public async Task SendsMultipleLogs(int? batchSize)
    {
        // Arrange
        var (logger, sink) = F.GetLogger(new OpenSearchSinkOptions
        {
            Tick = TimeSpan.FromMilliseconds(1),
            MaxBatchSize = batchSize
        });

        // Act
        for (var i = 0; i < 100; i++)
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            logger.Information("test {I}", i);

        // Assert
        await Task.Delay(2000);
        var result = await sink.Client.SearchAsync<AppFactSerilogOpenSearchEvent>(s => s.Size(150).Query(q => q.MatchAll()));
        result.Total.Should().Be(100);
        var logs = result.Documents.Select(l => l.Message).ToHashSet();
        logs.Should().Equal(Enumerable.Range(0, 100).Select(i => $"test {i}").ToHashSet());
    }

    [Fact]
    public async Task SkipsWaitingForTickWhenShuttingDownAndSendsRemainingLogs()
    {
        // Arrange
        var (logger, sink) = F.GetLogger(new OpenSearchSinkOptions()
            {
                Tick = TimeSpan.FromMinutes(10)
            }
        );
        await Task.Delay(1000); // wait for first tick to pass

        // Act
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        logger.Information("test");
        sink.OnProcessExit(null!, null!);

        // Assert
        await Task.Delay(2000);
        var result = await sink.Client.SearchAsync<AppFactSerilogOpenSearchEvent>(s => s.Query(q => q.MatchAll()));
        result.Total.Should().Be(1);
        result.Documents.First().Message.Should().Be("test");
    }
}