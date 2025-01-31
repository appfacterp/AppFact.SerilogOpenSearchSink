using FluentAssertions;

namespace AppFact.SerilogOpenSearchSink.Tests.Sink;

public class BasicTests : TestBase
{
    public BasicTests(TestFixture f) : base(f)
    {
    }

    [Fact]
    public async Task SendsLogsToOpenSearch()
    {
        // Arrange
        var (logger, sink) = F.GetLogger();
        var message = "hello";

        // Act
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        logger.Information(message);
        sink.Dispose();
        await Task.Delay(2000);

        // Assert
        var result = await sink.Client.SearchAllAsJson();
        await VerifyJson(result.ToJsonString());
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
        logger.Information("test");
        sink.Dispose();
        await Task.Delay(2000);

        // Assert
        var result = await sink.Client.SearchAllAsJson();
        await VerifyJson(result.ToJsonString());
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
            logger.Information("test {I}", i);
        
        sink.Dispose();
        await Task.Delay(2000);

        // Assert
        var result = await sink.Client.SearchAllAsJson();
        result.Count.Should().Be(100);
        await VerifyJson(result.ToJsonString()).UseParameters(batchSize);
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
        logger.Information("test");
        sink.OnProcessExit(null!, null!);
        await Task.Delay(2000);

        // Assert
        var result = await sink.Client.SearchAllAsJson();
        await VerifyJson(result.ToJsonString());
    }
}