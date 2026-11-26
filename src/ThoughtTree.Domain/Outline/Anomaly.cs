namespace ThoughtTree.Domain.Outline;

/// <summary>Why an event was not carried out as written (ADR-004, ADR-016, ADR-023; owner decisions 2026-11-26).</summary>
public enum AnomalyKind
{
    /// <summary>A second creation of an item id that exists; not applied.</summary>
    DuplicateCreate,

    /// <summary>A change to an item that does not exist at that point of the replay; not applied.</summary>
    UnknownItem,

    /// <summary>A parent that does not exist: a move is not applied, a new item goes to top level.</summary>
    UnknownParent,

    /// <summary>An afterItemId that does not exist or is the item itself: a move is not applied, a new item goes first.</summary>
    UnknownAfter,

    /// <summary>A move under the item itself or one of its descendants; not applied.</summary>
    Cycle,

    /// <summary>An event of a type this app does not know; skipped.</summary>
    UnknownEventType,
}

/// <summary>An event the projection did not carry out as written, with the item it names; the event stays in the log (ADR-005).</summary>
public sealed record Anomaly(AnomalyKind Kind, string EventId, ItemId? Item);
