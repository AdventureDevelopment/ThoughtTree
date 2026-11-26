using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Outline;

/// <summary>The id of an item, lowercase GUID text.</summary>
public readonly record struct ItemId(string Value)
{
    public override string ToString() => this.Value;
}

/// <summary>An immutable fact about a tree (ADR-002).</summary>
public abstract record TreeEvent(string EventId, string DeviceId, EventClock Clock, string ActionId)
{
    // Sealed, or every event record would print its titles and notes into logs.
    public sealed override string ToString() => $"{this.GetType().Name} {this.EventId}";
}

/// <summary>Creates an item under a parent after a sibling (ADR-004).</summary>
public sealed record ItemCreated(string EventId, string DeviceId, EventClock Clock, string ActionId,
    ItemId Item, ItemId? Parent, ItemId? After, string Title, string Note = "")
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Sets an item's title.</summary>
public sealed record ItemTitleChanged(string EventId, string DeviceId, EventClock Clock, string ActionId,
    ItemId Item, string Title)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Sets an item's note.</summary>
public sealed record ItemNoteChanged(string EventId, string DeviceId, EventClock Clock, string ActionId,
    ItemId Item, string Note)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Moves an item under a parent after a sibling (ADR-004, ADR-023).</summary>
public sealed record ItemMoved(string EventId, string DeviceId, EventClock Clock, string ActionId,
    ItemId Item, ItemId? Parent, ItemId? After)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Marks an item deleted (ADR-005).</summary>
public sealed record ItemDeleted(string EventId, string DeviceId, EventClock Clock, string ActionId, ItemId Item)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);

/// <summary>Clears an item's own deleted mark (ADR-005).</summary>
public sealed record ItemRestored(string EventId, string DeviceId, EventClock Clock, string ActionId, ItemId Item)
    : TreeEvent(EventId, DeviceId, Clock, ActionId);
