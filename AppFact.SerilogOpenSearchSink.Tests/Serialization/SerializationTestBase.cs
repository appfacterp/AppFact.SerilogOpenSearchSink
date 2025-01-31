using System.Text.Json;
using AppFact.SerilogOpenSearchSink.Serialization;
using OpenSearch.Net;

namespace AppFact.SerilogOpenSearchSink.Tests.Serialization;

public abstract class SerializationTestBase
{
    private readonly OpenSearchSerializer _serializer = OpenSearchSerializer.Instance;

    protected string Serialize<T>(T value)
    {
        using var memoryStream = new MemoryStream();
        _serializer.Serialize(value, memoryStream, SerializationFormatting.None);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        return reader.ReadToEnd();
    }

    internal AppFactSerilogOpenSearchEvent EventFactory()
    {
        try
        {
            throw new Exception("hello");
        }
        catch (Exception e)
        {
            return new AppFactSerilogOpenSearchEvent
            {
                Exception = e,
                Level = "hi",
                Message = "uwu",
                Template = "owo",
                Timestamp = DateTimeOffset.MinValue,
                Props = new Dictionary<string, object>()
                {
                    ["a"] = new MyClass()
                    {
                        Name = "b"
                    }
                }
            };
        }
    }

    class MyClass
    {
        public required string Name { get; init; }
    }
}