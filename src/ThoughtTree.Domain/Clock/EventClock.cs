namespace ThoughtTree.Domain.Clock;

/// <summary>Hybrid logical clock value of an event: one number in UTC milliseconds (ADR-014).</summary>
public readonly record struct EventClock(long Value) : IComparable<EventClock>
{
    public int CompareTo(EventClock other) => this.Value.CompareTo(other.Value);

    public static bool operator <(EventClock left, EventClock right) => left.CompareTo(right) < 0;
    public static bool operator >(EventClock left, EventClock right) => left.CompareTo(right) > 0;
    public static bool operator <=(EventClock left, EventClock right) => left.CompareTo(right) <= 0;
    public static bool operator >=(EventClock left, EventClock right) => left.CompareTo(right) >= 0;
}
