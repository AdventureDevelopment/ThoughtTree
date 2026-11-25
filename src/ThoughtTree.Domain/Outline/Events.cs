using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Outline;

/// <summary>Ids are lowercase GUID text; ordinal comparison (ADR-014).</summary>
public readonly record struct ItemId(string Value)
{
    public override string ToString() => this.Value;
}

/// <summary>An immutable fact about a tree (ADR-002). Header fields are shared by every event.</summary>
public abstract record TreeEvent(string EventId, string DeviceId, EventClock Clock, string ActionId);

/// <summary>Empty <paramref name="Parent"/> means top level; empty <paramref name="After"/> means first (ADR-004).</summary>
public sealed record ItemCreated(string EventId, string DeviceId, EventClock Clock, string ActionId,
    ItemId Item, ItemId? Parent, ItemId? After, string Title, string Note = "")
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

public sealed record ItemTitleChanged(string EventId, string DeviceId, EventClock Clock, string ActionId,
    ItemId Item, string Title)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

public sealed record ItemNoteChanged(string EventId, string DeviceId, EventClock Clock, string ActionId,
    ItemId Item, string Note)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

public sealed record ItemMoved(string EventId, string DeviceId, EventClock Clock, string ActionId,
    ItemId Item, ItemId? Parent, ItemId? After)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

public sealed record ItemDeleted(string EventId, string DeviceId, EventClock Clock, string ActionId, ItemId Item)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

public sealed record ItemRestored(string EventId, string DeviceId, EventClock Clock, string ActionId, ItemId Item)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);
