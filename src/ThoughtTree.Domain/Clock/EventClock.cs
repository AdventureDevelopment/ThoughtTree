namespace ThoughtTree.Domain.Clock;

/// <summary>Hybrid logical clock value of an event: one number in UTC milliseconds (ADR-014).</summary>
public readonly record struct EventClock(long Value);
