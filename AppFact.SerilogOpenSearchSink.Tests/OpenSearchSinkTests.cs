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
        var (logger, client) = F.GetLogger();
        var message = Guid.NewGuid().ToString();

        // Act
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        logger.Information(message);

        // Assert
        await Task.Delay(2000);
        var result = await client.SearchAsync<OpenSearchSink.Event>(s => s.Query(q => q.MatchAll()));
        result.Total.Should().Be(1);
        result.Documents.First().Message.Should().Be(message);
    }

    [Fact]
    public async Task SendsLogsInDefinedFormat()
    {
        // Arrange
        var (logger, client) = F.GetLogger(new OpenSearchSinkOptions()
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
        var result = await client.SearchAsync<TestEvent>(s => s.Query(q => q.MatchAll()));
        result.Total.Should().Be(1);
        result.Documents.First().Message.Should().Be("test test234");
        result.Documents.First().Abc.Should().Be("abc");
    }

    private class TestEvent
    {
        public required string Message { get; init; }
        public required string Abc { get; init; }
    }

    [Fact]
    public async Task SendsMultipleLogs()
    {
        // Arrange
        var (logger, client) = F.GetLogger();

        // Act
        for (var i = 0; i < 100; i++)
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            logger.Information("test {I}", i);

        // Assert
        await Task.Delay(2000);
        var result = await client.SearchAsync<OpenSearchSink.Event>(s => s.Size(150).Query(q => q.MatchAll()));
        result.Total.Should().Be(100);
        var logs = result.Documents.Select(l => l.Message).ToHashSet();
        logs.Should().Equal(Enumerable.Range(0, 100).Select(i => $"test {i}").ToHashSet());
    }
}