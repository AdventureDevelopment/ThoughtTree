namespace ThoughtTree.Domain.Clock;

/// <summary>Stamps the clock of a new event (ADR-014, INV-8).</summary>
public static class HybridClock
{
    /// <param name="now">The device's UTC time in milliseconds (ADR-034).</param>
    public static EventClock Next(long now, EventClock? highestInLog)
    {
        if (highestInLog is not { } highest || now > highest.Value)
        {
            return new EventClock(now);
        }

        var largestClockReached = highest.Value == long.MaxValue;
        return largestClockReached ? highest : new EventClock(highest.Value + 1);
    }
}
