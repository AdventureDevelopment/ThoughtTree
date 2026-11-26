using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Outline;

/// <summary>The agreed order of events: clock, then event id, ordinal (ADR-014).</summary>
public readonly record struct EventKey(EventClock Clock, EventId EventId)
{
    public static EventKey Of(TreeEvent e) => new(e.Clock, e.EventId);

    public static IComparer<EventKey> Comparer { get; } = Comparer<EventKey>.Create(static (a, b) =>
    {
        var byClock = a.Clock.Value.CompareTo(b.Clock.Value);
        return byClock != 0 ? byClock : string.CompareOrdinal(a.EventId.Value, b.EventId.Value);
    });

    internal static IComparer<TreeEvent> AgreedOrder { get; } = Comparer<TreeEvent>.Create(static (a, b) => Comparer.Compare(Of(a), Of(b)));
}
