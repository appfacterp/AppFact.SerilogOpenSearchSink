namespace AppFact.SerilogOpenSearchSink.Tests.Sink;

public class ExceptionTests : TestBase
{
    public ExceptionTests(TestFixture f) : base(f)
    {
    }

    [Fact]
    public async Task ShouldLogException()
    {
        // Arrange
        var (logger, sink) = F.GetLogger();

        // Act
        try
        {
            throw new Exception("uwu");
        }
        catch (Exception e)
        {
            logger.Error(e, "An error occurred");
        }

        sink.Dispose();
        await Task.Delay(1000);

        // Assert
        var result = await sink.Client.SearchAllAsJson();
        await VerifyJson(result.ToJsonString());
    }
}