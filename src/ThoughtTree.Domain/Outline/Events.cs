using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Outline;

/// <summary>An immutable fact about a tree (ADR-002).</summary>
public abstract record TreeEvent(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId)
{
    public sealed override string ToString() => $"{this.GetType().Name} {this.EventId}";
}

/// <summary>An event about one item, named by its id (ADR-004).</summary>
public abstract record ItemEvent(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId, ItemId ItemId)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Creates an item under a parent after a sibling (ADR-004); a missing title or note starts empty (ADR-016).</summary>
public sealed record ItemCreated(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId,
    ItemId ItemId, ItemId? ParentId, ItemId? AfterItemId, Title? Title = null, Note? Note = null)
    : ItemEvent(EventId, DeviceId, Clock, ActionId, ItemId);

/// <summary>Sets an item's title; a missing title changes nothing (ADR-016).</summary>
public sealed record ItemTitleChanged(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId,
    ItemId ItemId, Title? Title)
    : ItemEvent(EventId, DeviceId, Clock, ActionId, ItemId);

/// <summary>Sets an item's note; a missing note changes nothing (ADR-016).</summary>
public sealed record ItemNoteChanged(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId,
    ItemId ItemId, Note? Note)
    : ItemEvent(EventId, DeviceId, Clock, ActionId, ItemId);

/// <summary>Moves an item under a parent after a sibling (ADR-004, ADR-023).</summary>
public sealed record ItemMoved(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId,
    ItemId ItemId, ItemId? ParentId, ItemId? AfterItemId)
    : ItemEvent(EventId, DeviceId, Clock, ActionId, ItemId);

/// <summary>Marks an item deleted (ADR-005).</summary>
public sealed record ItemDeleted(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId, ItemId ItemId)
    : ItemEvent(EventId, DeviceId, Clock, ActionId, ItemId);

/// <summary>Clears an item's own deleted mark (ADR-005).</summary>
public sealed record ItemRestored(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId, ItemId ItemId)
    : ItemEvent(EventId, DeviceId, Clock, ActionId, ItemId);
