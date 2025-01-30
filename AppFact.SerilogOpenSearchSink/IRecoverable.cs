using OpenSearch.Net;

namespace AppFact.SerilogOpenSearchSink;

/// <summary>
/// Types that implement this interface can recover
/// when the serialization of the object fails.
/// </summary>
public interface IRecoverable
{
    /// <summary>
    /// Returns a recovered object.
    ///
    /// If the recovery fails, the method should return null.
    ///
    /// Recovered values should pass the <see cref="OpenSearchSerializerExtensions.CanSerialize{T}"/> check.
    /// </summary>
    IRecoverable? Recover(IOpenSearchSerializer serializer);
}

internal static class RecoverableExtensions
{
    internal static IRecoverable? RecoverSafe(this IRecoverable recoverable, IOpenSearchSerializer serializer)
    {
        try
        {
            return recoverable.Recover(serializer);
        }
        catch
        {
            return default;
        }
    }
}

/// <summary>
/// Extension methods for <see cref="IOpenSearchSerializer"/>.
/// </summary>
public static class OpenSearchSerializerExtensions
{
    /// <summary>
    /// Checks if the serializer can serialize the value.
    ///
    /// !!! IMPORTANT: this method only checks if the root object can be serialized.
    /// Child Properties are not checked!!!!!!!!!!!
    /// </summary>
    public static bool CanSerialize<T>(this IOpenSearchSerializer serializer, T value)
    {
        try
        {
            serializer.Serialize(value, Stream.Null);
            return true;
        }
        catch
        {
            return false;
        }
    }
}