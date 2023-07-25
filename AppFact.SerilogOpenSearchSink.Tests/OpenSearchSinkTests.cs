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
}