using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppFact.SerilogOpenSearchSink.Serialization;

file class UnserializableObjectConverter<T> : JsonConverter<T>
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var text = "[[Failed to serialize object]]";
        try
        {
            text = value.ToString();
        }
        catch
        {
            // ignored
        }

        writer.WriteStringValue(text);
    }
}

internal class UnserializableObjectConverterFactory : JsonConverterFactory
{
    private static readonly Lazy<JsonConverterFactory> UnsupportedTypeConverterFactory =
        new(() =>
        {
            var typeName = "System.Text.Json.Serialization.Converters.UnsupportedTypeConverterFactory";
            var type = typeof(JsonConverter).Assembly.GetType(typeName, true)
                       ?? throw new Exception($"Failed to find type {typeName}");
            return (JsonConverterFactory)Activator.CreateInstance(type)!;
        });

    public override bool CanConvert(Type typeToConvert)
    {
        var canConvert = UnsupportedTypeConverterFactory.Value.CanConvert(typeToConvert);
        return canConvert;
    }


    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions _)
    {
        var converterType = typeof(UnserializableObjectConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}