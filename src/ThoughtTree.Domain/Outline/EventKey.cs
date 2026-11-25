using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Outline;

/// <summary>The replay order of a tree: clock, then event id as text, ordinal (ADR-014). Distinct events never compare equal.</summary>
public readonly record struct EventKey(EventClock Clock, string EventId)
{
    public static EventKey Of(TreeEvent e) => new(e.Clock, e.EventId);

    public static IComparer<EventKey> Comparer { get; } = Comparer<EventKey>.Create(static (a, b) =>
    {
        var byClock = a.Clock.Value.CompareTo(b.Clock.Value);
        return byClock != 0 ? byClock : string.CompareOrdinal(a.EventId, b.EventId);
    });
}
