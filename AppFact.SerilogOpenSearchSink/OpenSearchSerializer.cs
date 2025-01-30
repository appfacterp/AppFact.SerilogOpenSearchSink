using System.Text.Json;
using OpenSearch.Client;
using OpenSearch.Net;

namespace AppFact.SerilogOpenSearchSink;

/// <summary>
/// Implementation of <see cref="IOpenSearchSerializer"/> that uses <see cref="JsonSerializer"/> for serialization.
/// </summary>
public class OpenSearchSerializer : IOpenSearchSerializer
{
    private static OpenSearchSerializer Default { get; } = new(new JsonSerializerOptions());

/// <summary>
    /// Factory method for creating a <see cref="OpenSearchSerializer"/> instance.
    /// </summary>
    public static ConnectionSettings.SourceSerializerFactory SourceSerializerFactory
        => (_, _) => Default;


    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Creates a new instance of <see cref="OpenSearchSerializer"/>.
    /// </summary>
    /// <param name="options"></param>
    public OpenSearchSerializer(JsonSerializerOptions options)
    {
        _options = options;
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