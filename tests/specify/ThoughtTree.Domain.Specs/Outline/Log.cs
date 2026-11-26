using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

/// <summary>Writes events with counted ids and clocks (ADR-034).</summary>
internal sealed class Log(string device = "a0000000-0000-0000-0000-000000000001")
{
    private int count;

    public List<TreeEvent> Events { get; } = [];

    public static ItemId Id(string name) => new(name);

    private static ItemId? OptionalId(string? name) => name is null ? null : Id(name);

    private (EventId EventId, DeviceId Device, EventClock Clock, ActionId Action) Next(long time)
    {
        this.count++;
        return (new EventId($"{device}-{this.count}"), new DeviceId(device), new EventClock(time), new ActionId($"action-{this.count}"));
    }

    public Log Create(long time, string item, string title, string? parent = null, string? after = null)
    {
        var header = this.Next(time);
        this.Events.Add(new ItemCreated(header.EventId, header.Device, header.Clock, header.Action, Id(item),
            OptionalId(parent), OptionalId(after), new Title(title)));
        return this;
    }

    public Log Rename(long time, string item, string title)
    {
        var header = this.Next(time);
        this.Events.Add(new ItemTitleChanged(header.EventId, header.Device, header.Clock, header.Action, Id(item), new Title(title)));
        return this;
    }

    public Log ChangeNote(long time, string item, string note)
    {
        var header = this.Next(time);
        this.Events.Add(new ItemNoteChanged(header.EventId, header.Device, header.Clock, header.Action, Id(item), new Note(note)));
        return this;
    }

    public Log Move(long time, string item, string? parent, string? after = null)
    {
        var header = this.Next(time);
        this.Events.Add(new ItemMoved(header.EventId, header.Device, header.Clock, header.Action, Id(item),
            OptionalId(parent), OptionalId(after)));
        return this;
    }

    public Log Delete(long time, string item)
    {
        var header = this.Next(time);
        this.Events.Add(new ItemDeleted(header.EventId, header.Device, header.Clock, header.Action, Id(item)));
        return this;
    }

    public Log Restore(long time, string item)
    {
        var header = this.Next(time);
        this.Events.Add(new ItemRestored(header.EventId, header.Device, header.Clock, header.Action, Id(item)));
        return this;
    }
}
