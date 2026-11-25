namespace ThoughtTree.Domain.Outline;

public enum AnomalyKind
{
    /// <summary>Same device and same clock as an earlier event with another id; rejected for the user to review.</summary>
    SameClock,

    /// <summary>A second creation of an item id that exists; kept in the log, not applied.</summary>
    DuplicateCreate,

    /// <summary>A change to an item that does not exist at that point of the replay; kept in the log, not applied.</summary>
    UnknownItem,
}

/// <summary>An event the projection did not apply. The event stays in the log (ADR-005: nothing is removed).</summary>
public sealed record Anomaly(AnomalyKind Kind, string EventId);
