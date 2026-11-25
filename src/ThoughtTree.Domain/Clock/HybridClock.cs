namespace ThoughtTree.Domain.Clock;

/// <summary>Stamps new events. The clock is never stored: it derives from the highest clock in the local log (ADR-014, INV-8).</summary>
public static class HybridClock
{
    /// <param name="now">The device's current UTC time in milliseconds, supplied by the caller (ADR-034).</param>
    /// <param name="highestInLog">The highest clock in the tree's local log, or null for an empty log.</param>
    public static EventClock Next(long now, EventClock? highestInLog)
    {
        if (highestInLog is not { } highest || now > highest.Time)
        {
            return new EventClock(now, 0);
        }

        // ADR-014: far-ahead clocks are accepted, never capped; a full counter rolls into the next millisecond.
        return highest.Counter == int.MaxValue
            ? new EventClock(highest.Time + 1, 0)
            : new EventClock(highest.Time, highest.Counter + 1);
    }
}
