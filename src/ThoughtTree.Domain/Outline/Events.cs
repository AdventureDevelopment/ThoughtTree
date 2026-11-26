using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Outline;

/// <summary>The id of an item, lowercase GUID text.</summary>
public readonly record struct ItemId(string Value)
{
    public override string ToString() => this.Value;
}

/// <summary>The id of an event, unique in a tree; replay ties are ordered by it (ADR-014).</summary>
public readonly record struct EventId(string Value)
{
    public override string ToString() => this.Value;
}

/// <summary>The id of the device that wrote an event.</summary>
public readonly record struct DeviceId(string Value)
{
    public override string ToString() => this.Value;
}

/// <summary>The id shared by all events of one user action (ADR-067).</summary>
public readonly record struct ActionId(string Value)
{
    public override string ToString() => this.Value;
}

/// <summary>An immutable fact about a tree (ADR-002).</summary>
public abstract record TreeEvent(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId)
{
    public sealed override string ToString() => $"{this.GetType().Name} {this.EventId}";
}

/// <summary>Creates an item under a parent after a sibling (ADR-004); a missing title or note starts empty (ADR-016).</summary>
public sealed record ItemCreated(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId,
    ItemId ItemId, ItemId? ParentId, ItemId? AfterItemId, Title? Title, Note? Note = null)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Sets an item's title; a missing title changes nothing (ADR-016).</summary>
public sealed record ItemTitleChanged(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId,
    ItemId ItemId, Title? Title)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Sets an item's note; a missing note changes nothing (ADR-016).</summary>
public sealed record ItemNoteChanged(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId,
    ItemId ItemId, Note? Note)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Moves an item under a parent after a sibling (ADR-004, ADR-023).</summary>
public sealed record ItemMoved(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId,
    ItemId ItemId, ItemId? ParentId, ItemId? AfterItemId)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Marks an item deleted (ADR-005).</summary>
public sealed record ItemDeleted(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId, ItemId ItemId)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Clears an item's own deleted mark (ADR-005).</summary>
public sealed record ItemRestored(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId, ItemId ItemId)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);
