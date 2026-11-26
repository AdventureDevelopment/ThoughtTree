using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Outline;

/// <summary>The agreed order of events: clock, then event id, ordinal (ADR-014).</summary>
public readonly record struct EventKey(EventClock Clock, EventId EventId)
{
    public static EventKey Of(TreeEvent treeEvent) => new(treeEvent.Clock, treeEvent.EventId);

    public static IComparer<EventKey> Comparer { get; } = Comparer<EventKey>.Create(static (first, second) =>
    {
        var byClock = first.Clock.Value.CompareTo(second.Clock.Value);
        return byClock != 0 ? byClock : string.CompareOrdinal(first.EventId.Value, second.EventId.Value);
    });

    internal static IComparer<TreeEvent> AgreedOrder { get; } = Comparer<TreeEvent>.Create(static (first, second) => Comparer.Compare(Of(first), Of(second)));
}
