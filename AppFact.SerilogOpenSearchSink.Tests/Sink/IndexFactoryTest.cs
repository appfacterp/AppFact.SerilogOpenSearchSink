namespace AppFact.SerilogOpenSearchSink.Tests.Sink;

public class IndexFactoryTest(TestFixture f) : TestBase(f)
{
    [Fact]
    public async Task IndexFromFactory()
    {
        // Arrange
        var index = $"index-{Guid.NewGuid()}";
        var (logger, sink) = F.GetLogger(new OpenSearchSinkOptions
        {
            IndexNameFactory = () => index
        });

        // Act
        logger.Information("Hello there!!");
        sink.Dispose();
        await Task.Delay(1000);

        // Assert
        var result = await sink.Client.SearchAllAsJson(index);
        await VerifyJson(result.ToJsonString());
    }
}