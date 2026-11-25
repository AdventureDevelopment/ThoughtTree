namespace ThoughtTree.Domain.Outline;

public enum AnomalyKind
{
    // Declared in alphabetical order of their names: anomalies are listed in this order (model contract).
    /// <summary>A second creation of an item id that exists; kept in the log, not applied.</summary>
    DuplicateCreate,

    /// <summary>A change to an item that does not exist at that point of the replay; kept in the log, not applied.</summary>
    UnknownItem,
}

/// <summary>An event the user should review. Duplicate creations and changes to unknown items are not applied; the event stays in the log (ADR-005).</summary>
public sealed record Anomaly(AnomalyKind Kind, string EventId);
