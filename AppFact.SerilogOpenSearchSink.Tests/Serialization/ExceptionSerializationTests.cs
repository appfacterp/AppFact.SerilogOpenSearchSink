namespace AppFact.SerilogOpenSearchSink.Tests.Serialization;

public class ExceptionSerializationTests : SerializationTestBase
{
    [Fact]
    public async Task CanSerializeSimpleException()
    {
        // Arrange

        Exception exception;
        try
        {
            // unthrown exception is boring to serialize
            throw new Exception("Test exception");
        }
        catch (Exception e)
        {
            exception = e;
        }

        // Act
        var serialized = Serialize(exception);

        // Assert
        await VerifyJson(serialized);
    }

    [Fact]
    public async Task CanSerializeNestedException()
    {
        // Arrange

        Exception exception;
        try
        {
            // unthrown exception is boring to serialize
            throw new Exception("Inner Exceptioon");
        }
        catch (Exception e)
        {
            try
            {
                throw new Exception("Outer Exception", e);
            }
            catch (Exception e1)
            {
                exception = e1;
            }
        }

        // Act
        var serialized = Serialize(exception);

        // Assert
        await VerifyJson(serialized);
    }
}