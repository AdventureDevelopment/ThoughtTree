namespace ThoughtTree.Domain.Clock;

/// <summary>Stamps the clock of a new event (ADR-014, INV-8).</summary>
public static class HybridClock
{
    public static EventClock Next(DateTimeOffset now, EventClock? highestInLog)
    {
        var nowInMilliseconds = now.ToUnixTimeMilliseconds();
        if (highestInLog is not { } highest || nowInMilliseconds > highest.Value)
        {
            return new EventClock(nowInMilliseconds);
        }

        var largestClockReached = highest.Value == long.MaxValue;
        return largestClockReached ? highest : new EventClock(highest.Value + 1);
    }
}
