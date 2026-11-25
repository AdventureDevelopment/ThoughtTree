using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Outline;

/// <summary>The full replay order of an event: clock, device id, then event id (ADR-014). Distinct events never compare equal.</summary>
public readonly record struct EventKey(EventClock Clock, string DeviceId, string EventId)
{
    public static EventKey Of(TreeEvent e) => new(e.Clock, e.DeviceId, e.EventId);

    public static IComparer<EventKey> Comparer { get; } = Comparer<EventKey>.Create(static (a, b) =>
    {
        int byClockAndDevice = EventOrder.Instance.Compare((a.Clock, a.DeviceId), (b.Clock, b.DeviceId));
        return byClockAndDevice != 0 ? byClockAndDevice : string.CompareOrdinal(a.EventId, b.EventId);
    });
}
