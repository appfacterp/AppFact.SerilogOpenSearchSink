using Serilog.Events;

namespace AppFact.SerilogOpenSearchSink;

internal class AppFactSerilogOpenSearchEvent
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public required string Template { get; init; }
    public required IDictionary<string, object> Props { get; init; }
    public required Exception? Exception { get; init; }
    
    

    public static object MapEvent(LogEvent e)
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