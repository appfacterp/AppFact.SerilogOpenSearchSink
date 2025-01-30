using OpenSearch.Net;

namespace AppFact.SerilogOpenSearchSink;

internal class AppFactSerilogOpenSearchEvent : IRecoverable<AppFactSerilogOpenSearchEvent>
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public required string Template { get; init; }
    public required IDictionary<string, object> Props { get; init; }
    public required Exception? Exception { get; init; }


    public AppFactSerilogOpenSearchEvent? Recover(IOpenSearchSerializer serializer)
    {
        var ex = serializer.CanSerialize(Exception)
            ? Exception
            : null;

        var ev = new AppFactSerilogOpenSearchEvent
        {
            Timestamp = Timestamp,
            Exception = ex,
            Level = Level,
            Message = Message,
            Template = Template,
            Props = new Dictionary<string, object>()
        };

        foreach (var kv in Props)
        {
            if (serializer.CanSerialize(kv.Value))
            {
                ev.Props.Add(kv.Key, kv.Value);
                continue;
            }
            
            var value = "[[[failed to serialize]]]";
            try
            {
                value = kv.Value?.ToString() ?? "[[[null]]]";
            }
            catch
            {
                // ignored
            }
            
            ev.Props.Add(kv.Key, value);
        }

        if (Exception is not null && ex is null)
        {
            var exKey = "Exception";
            if (ev.Props.ContainsKey(exKey))
                exKey += Guid.NewGuid().ToString();

            var exMsg = "[[[failed to serialize]]]";
            try
            {
                exMsg = Exception.ToString();
            }
            catch
            {
                // ignored
            }

            ev.Props.Add(exKey, exMsg);
        }

        return serializer.CanSerialize(ev)
            ? ev
            : null;
    }
}