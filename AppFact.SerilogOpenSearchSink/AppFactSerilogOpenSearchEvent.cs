using OpenSearch.Net;
using Serilog.Events;

namespace AppFact.SerilogOpenSearchSink;

internal class AppFactSerilogOpenSearchEvent : IRecoverable
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public required string Template { get; init; }
    public required IDictionary<string, object> Props { get; init; }
    public required Exception? Exception { get; init; }


    public IRecoverable? Recover(IOpenSearchSerializer serializer)
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
                ev.Props.Add(kv.Key, kv.Value!);
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

        return ev;
    }
    
    
    public static AppFactSerilogOpenSearchEvent MapEvent(LogEvent e)
    {
        var message = e.RenderMessage();
        var props = e.Properties
            .Where(p => p.Key != "EventId" && (p.Value is not ScalarValue { Value: null }))
            .ToDictionary(k => k.Key, v => v.Value switch
            {
                ScalarValue scalar => scalar.Value!,
                var value => value
            });

        return new AppFactSerilogOpenSearchEvent
        {
            Timestamp = e.Timestamp,
            Level = e.Level.ToString(),
            Message = message,
            Props = props,
            Template = e.MessageTemplate.Text,
            Exception = e.Exception
        };
    }

}