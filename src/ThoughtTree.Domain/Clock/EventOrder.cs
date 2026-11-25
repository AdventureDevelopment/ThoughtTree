namespace ThoughtTree.Domain.Clock;

/// <summary>The one replay order of a tree: clock, then device id as lowercase GUID text, ordinal (ADR-014).</summary>
public sealed class EventOrder : IComparer<(EventClock Clock, string DeviceId)>
{
    public static EventOrder Instance { get; } = new();

    public int Compare((EventClock Clock, string DeviceId) x, (EventClock Clock, string DeviceId) y)
    {
        var byClock = x.Clock.CompareTo(y.Clock);
        return byClock != 0 ? byClock : string.CompareOrdinal(x.DeviceId, y.DeviceId);
    }
}
