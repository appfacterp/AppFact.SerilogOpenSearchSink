using OpenSearch.Net;

namespace AppFact.SerilogOpenSearchSink;

/// <summary>
/// Types that implement this interface can recover
/// when the serialization of the object fails.
/// </summary>
public interface IRecoverable<out T>
{
    /// <summary>
    /// Returns a recovered object.
    ///
    /// If the recovery fails, the method should return null.
    ///
    /// Recovered values should not pass the <see cref="OpenSearchSerializerExtensions.CanSerialize{T}"/> check.
    /// </summary>
    T? Recover(IOpenSearchSerializer serializer);
}

internal static class RecoverableExtensions
{
    internal static T? RecoverSafe<T>(this IRecoverable<T> recoverable, IOpenSearchSerializer serializer)
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