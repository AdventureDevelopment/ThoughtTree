namespace ThoughtTree.Domain.Outline;

/// <summary>Why an event was not carried out as written (ADR-004, ADR-016, ADR-023).</summary>
public enum AnomalyKind
{
    DuplicateCreate,

    UnknownItem,

    UnknownParent,

    UnknownAfter,

    Cycle,

    UnknownEventType,
}

/// <summary>An event the projection did not carry out as written, and the item it names.</summary>
public sealed record Anomaly(AnomalyKind Kind, EventId EventId, ItemId? Item);
