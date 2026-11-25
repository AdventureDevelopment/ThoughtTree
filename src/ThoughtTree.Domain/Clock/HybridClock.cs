namespace ThoughtTree.Domain.Clock;

/// <summary>Stamps new events. The clock is never stored: it derives from the highest clock in the local log (ADR-014, INV-8).</summary>
public static class HybridClock
{
    /// <param name="now">The device's current UTC time in milliseconds, supplied by the caller (ADR-034).</param>
    /// <param name="highestInLog">The highest clock in the tree's local log, or null for an empty log.</param>
    public static EventClock Next(long now, EventClock? highestInLog)
    {
        if (highestInLog is not { } highest || now > highest.Value)
        {
            return new EventClock(now);
        }

        // ADR-014: at the largest value the new event ties, and the event id orders it.
        return highest.Value == long.MaxValue ? highest : new EventClock(highest.Value + 1);
    }
}
