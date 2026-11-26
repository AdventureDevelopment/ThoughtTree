using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

/// <summary>Writes events with counted ids and clocks (ADR-034).</summary>
internal sealed class Log(string device = "a0000000-0000-0000-0000-000000000001")
{
    private int count;

    public List<TreeEvent> Events { get; } = [];

    public static ItemId Id(string name) => new(name);

    // An event id is device plus count: two logs in one scenario need different devices.
    private (string EventId, string Device, EventClock Clock, string Action) Next(long time)
    {
        this.count++;
        return ($"{device}-{this.count}", device, new EventClock(time), $"action-{this.count}");
    }

    public Log Create(long time, string item, string title, string? parent = null, string? after = null)
    {
        var h = this.Next(time);
        this.Events.Add(new ItemCreated(h.EventId, h.Device, h.Clock, h.Action, Id(item),
            parent is null ? null : Id(parent), after is null ? null : Id(after), title));
        return this;
    }

    public Log Rename(long time, string item, string title)
    {
        var h = this.Next(time);
        this.Events.Add(new ItemTitleChanged(h.EventId, h.Device, h.Clock, h.Action, Id(item), title));
        return this;
    }

    public Log Note(long time, string item, string note)
    {
        var h = this.Next(time);
        this.Events.Add(new ItemNoteChanged(h.EventId, h.Device, h.Clock, h.Action, Id(item), note));
        return this;
    }

    public Log Move(long time, string item, string? parent, string? after = null)
    {
        var h = this.Next(time);
        this.Events.Add(new ItemMoved(h.EventId, h.Device, h.Clock, h.Action, Id(item),
            parent is null ? null : Id(parent), after is null ? null : Id(after)));
        return this;
    }

    public Log Delete(long time, string item)
    {
        var h = this.Next(time);
        this.Events.Add(new ItemDeleted(h.EventId, h.Device, h.Clock, h.Action, Id(item)));
        return this;
    }

    public Log Restore(long time, string item)
    {
        var h = this.Next(time);
        this.Events.Add(new ItemRestored(h.EventId, h.Device, h.Clock, h.Action, Id(item)));
        return this;
    }
}
