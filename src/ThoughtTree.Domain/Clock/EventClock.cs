namespace ThoughtTree.Domain.Clock;

/// <summary>Hybrid logical clock value of an event: UTC milliseconds plus a counter (ADR-014).</summary>
public readonly record struct EventClock(long Time, int Counter) : IComparable<EventClock>
{
    public int CompareTo(EventClock other)
    {
        int byTime = this.Time.CompareTo(other.Time);
        return byTime != 0 ? byTime : this.Counter.CompareTo(other.Counter);
    }

    public static bool operator <(EventClock left, EventClock right) => left.CompareTo(right) < 0;
    public static bool operator >(EventClock left, EventClock right) => left.CompareTo(right) > 0;
    public static bool operator <=(EventClock left, EventClock right) => left.CompareTo(right) <= 0;
    public static bool operator >=(EventClock left, EventClock right) => left.CompareTo(right) >= 0;
}
