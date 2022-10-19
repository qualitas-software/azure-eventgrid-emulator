namespace Qs.EventGrid.Emulator;

/// <summary>Top level options class.</summary>
public class Services : List<Service>
{
    /// <summary>Convert to lookup on event type, with <see cref="Service"/> and <see cref="Endpoint"/> values.</summary>
    internal EventTypeMap KeyByEventType(ILogger logger = null)
    {
        var eventTypeList = this.SelectMany(sub => sub.Endpoints.SelectMany(ep => ep.EventTypes.Select(evt => (ep, sub, evt)))).ToList();
        var eventTypeMap = new EventTypeMap(eventTypeList.GroupBy(l => l.evt).ToDictionary(k => k.Key, v => v.Select(v => new Subscription(v.ep, v.sub)).ToArray()));
        logger?.LogInformation("EventTypes: {EventTypes}", eventTypeMap.ToString());
        return eventTypeMap;
    }
}

public record Service(string BaseAddress, Endpoint[] Endpoints)
{
    public Service() : this(default, default) { }
}
public record Endpoint(string Path, string EventGridFunction, string[] EventTypes)
{
    public Endpoint() : this(default, default, default) { }
}
internal class EventTypeMap : Dictionary<string, Subscription[]>
{
    public EventTypeMap(IDictionary<string, Subscription[]> keyValuePairs) : base(keyValuePairs) { }

    public override string ToString()
        => $"{{\n{this.Aggregate("", (acc, ev) => $"{acc}  {ev.Key}: {ev.Value.Select(j => $"{j}")?.Aggregate((a, b) => $"{a}, {b}")}\n", s => s.TrimEnd('\n'))}\n}}";
}

internal record Subscription(Endpoint Endpoint, Service Service)
{
    public static string ToString(Subscription mappedSubscriber) => $"{mappedSubscriber}";

    public override string ToString()
        => $"{Service.BaseAddress.EnsureTrailing()}{Endpoint.EventGridFunction?.Prepend("EventGridFunc:") ?? Endpoint.Path}";

    public static implicit operator Subscription((Service, Endpoint) entity)
        => (entity);
}