namespace AppFact.SerilogOpenSearchSink.Tests.Serialization;

public class SimpleSerializationTests : SerializationTestBase
{
    [Fact]
    public async Task CanSerializeSimpleEvent()
    {
        // Arrange
        var ev = EventFactory();

        // Act
        var json = Serialize(ev);

        // Assert
        await VerifyJson(json.HandleSolutionPathsInString());
    }

    [Fact]
    public async Task CanSerializeList()
    {
        // Arrange
        var events = Enumerable.Range(0, 100).Select(_ => EventFactory())
            .ToList();

        // Act
        var json = Serialize(events);

        // Assert
        await VerifyJson(json.HandleSolutionPathsInString());
    }
}