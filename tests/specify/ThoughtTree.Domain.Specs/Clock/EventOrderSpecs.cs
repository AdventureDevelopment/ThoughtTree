using LambdaTale;
using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Specs.Clock;

/// <summary>
/// How a device stamps its events and how every device puts events in the same order (ADR-014, INV-8).
/// Time comes in as a value; the Domain never reads a clock itself (ADR-034).
/// </summary>
[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-014")]
public class EventOrderSpecs
{
    private const string DeviceA = "3f2a0000-0000-0000-0000-000000000001";
    private const string DeviceB = "aa000000-0000-0000-0000-000000000002";

    [Scenario]
    public void INV_8_A_new_event_gets_the_current_time_when_the_log_is_older()
    {
        EventClock stamp = default!;

        "Given a device whose log holds an event from one second ago"
            .x(() => { });

        "When the device writes a new event"
            .x(() => stamp = HybridClock.Next(now: 10_000, highestInLog: new EventClock(9_000, 4)));

        "Then the event carries the current time with counter zero"
            .x(() => stamp.ShouldBe(new EventClock(10_000, 0)));
    }

    [Scenario]
    public void INV_8_A_new_event_counts_on_when_the_log_is_equal_or_ahead()
    {
        EventClock sameTime = default!;
        EventClock ahead = default!;

        "Given a device whose log holds an event stamped with the current time"
            .x(() => { });

        "When the device writes a new event"
            .x(() => sameTime = HybridClock.Next(now: 10_000, highestInLog: new EventClock(10_000, 2)));

        "And it writes one while the log holds an event from a clock three days ahead"
            .x(() => ahead = HybridClock.Next(now: 10_000, highestInLog: new EventClock(10_000 + 259_200_000, 0)));

        "Then the new event comes after the logged one with the counter raised by one"
            .x(() => sameTime.ShouldBe(new EventClock(10_000, 3)));

        "And the event after a far-ahead clock counts on from that clock instead of being rejected"
            .x(() => ahead.ShouldBe(new EventClock(10_000 + 259_200_000, 1)));
    }

    [Scenario]
    public void A_device_with_an_empty_log_uses_the_current_time()
    {
        EventClock stamp = default!;

        "Given a device with an empty log"
            .x(() => { });

        "When the device writes its first event"
            .x(() => stamp = HybridClock.Next(now: 5, highestInLog: null));

        "Then the event carries the current time with counter zero"
            .x(() => stamp.ShouldBe(new EventClock(5, 0)));
    }

    [Scenario]
    public void Events_are_ordered_by_clock_and_then_by_device()
    {
        var events = new List<(EventClock Clock, string Device)>();
        List<(EventClock Clock, string Device)> ordered = default!;

        "Given events from two devices, one pair with equal clocks and one with a higher counter"
            .x(() =>
            {
                events.Add((new EventClock(7, 1), DeviceA));
                events.Add((new EventClock(7, 0), DeviceB));
                events.Add((new EventClock(7, 0), DeviceA));
                events.Add((new EventClock(6, 9), DeviceB));
            });

        "When the events are put in the agreed order"
            .x(() => ordered = events.Order(EventOrder.Instance).ToList());

        "Then they run by time, then counter, then lowercase device text"
            .x(() => ordered.ShouldBe(
            [
                (new EventClock(6, 9), DeviceB),
                (new EventClock(7, 0), DeviceA),
                (new EventClock(7, 0), DeviceB),
                (new EventClock(7, 1), DeviceA),
            ]));
    }
}
