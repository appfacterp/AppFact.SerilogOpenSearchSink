using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Client;
using OpenSearch.Net;

namespace AppFact.SerilogOpenSearchSink.Serialization;

/// <summary>
/// Implementation of <see cref="IOpenSearchSerializer"/> that uses <see cref="JsonSerializer"/> for serialization.
/// </summary>
public class OpenSearchSerializer : IOpenSearchSerializer
{
    /// <summary>
    /// Singleton instance of <see cref="OpenSearchSerializer"/>.
    /// </summary>
    public static OpenSearchSerializer Instance { get; } = new();

    /// <summary>
    /// Factory method for creating a <see cref="OpenSearchSerializer"/> instance.
    /// </summary>
    public static ConnectionSettings.SourceSerializerFactory SourceSerializerFactory
        => (_, _) => Instance;


    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Creates a new instance of <see cref="OpenSearchSerializer"/>.
    /// </summary>
    private OpenSearchSerializer()
    {
        _options = new JsonSerializerOptions()
        {
            Converters = { new UnserializableObjectConverterFactory() },
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
    }

    /// <inheritdoc />
    public object Deserialize(Type type, Stream stream)
    {
        return JsonSerializer.Deserialize(stream, type, _options)
               ?? throw new InvalidOperationException("Deserialization failed");
    }

    /// <inheritdoc />
    public T Deserialize<T>(Stream stream)
    {
        return (T)Deserialize(typeof(T), stream);
    }

    /// <inheritdoc />
    public async Task<object> DeserializeAsync(Type type, Stream stream,
        CancellationToken cancellationToken)
    {
        return await JsonSerializer.DeserializeAsync(stream, type, _options, cancellationToken)
               ?? throw new InvalidOperationException("Deserialization failed");
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(Stream stream,
        CancellationToken cancellationToken)
    {
        return (T)await DeserializeAsync(typeof(T), stream, cancellationToken);
    }

    /// <inheritdoc />
    public void Serialize<T>(T data, Stream stream, SerializationFormatting formatting)
    {
        JsonSerializer.Serialize(stream, data, _options);
    }

    /// <inheritdoc />
    public async Task SerializeAsync<T>(T data, Stream stream,
        SerializationFormatting formatting,
        CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(stream, data, _options, cancellationToken);
    }
}