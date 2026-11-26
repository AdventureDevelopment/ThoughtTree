namespace ThoughtTree.Domain.Outline;

/// <summary>An event the projection did not carry out as written, and the item it names.</summary>
public sealed record Anomaly(AnomalyKind Kind, EventId EventId, ItemId? ItemId);
