using LambdaTale;
using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Clock;

/// <summary>How a device stamps its events and how every device orders them (ADR-014, INV-8).</summary>
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
            .x(() => stamp = HybridClock.Next(now: 10_000, highestInLog: new EventClock(9_000)));

        "Then the event carries the current time"
            .x(() => stamp.ShouldBe(new EventClock(10_000)));
    }

    [Scenario]
    public void INV_8_A_new_event_counts_on_by_one_when_the_log_is_equal_or_ahead()
    {
        EventClock sameTime = default!;
        EventClock ahead = default!;

        "Given a device whose log holds an event stamped with the current time"
            .x(() => { });

        "When the device writes a new event"
            .x(() => sameTime = HybridClock.Next(now: 10_000, highestInLog: new EventClock(10_000)));

        "And it writes one while the log holds an event from a clock three days ahead"
            .x(() => ahead = HybridClock.Next(now: 10_000, highestInLog: new EventClock(10_000 + 259_200_000)));

        "Then the new event comes one after the logged one"
            .x(() => sameTime.ShouldBe(new EventClock(10_001)));

        "And the event after a far-ahead clock counts on from that clock instead of being rejected"
            .x(() => ahead.ShouldBe(new EventClock(10_000 + 259_200_000 + 1)));
    }

    [Scenario]
    public void A_device_with_an_empty_log_uses_the_current_time()
    {
        EventClock stamp = default!;

        "Given a device with an empty log"
            .x(() => { });

        "When the device writes its first event"
            .x(() => stamp = HybridClock.Next(now: 5, highestInLog: null));

        "Then the event carries the current time"
            .x(() => stamp.ShouldBe(new EventClock(5)));
    }

    [Scenario]
    public void Events_are_ordered_by_clock_and_then_by_event_id()
    {
        var events = new List<EventKey>();
        List<EventKey> ordered = default!;

        "Given events with equal and different clocks"
            .x(() =>
            {
                events.Add(new EventKey(new EventClock(8), "e1"));
                events.Add(new EventKey(new EventClock(7), "e3"));
                events.Add(new EventKey(new EventClock(7), "e2"));
                events.Add(new EventKey(new EventClock(6), "e4"));
            });

        "When the events are put in the agreed order"
            .x(() => ordered = events.Order(EventKey.Comparer).ToList());

        "Then they run by clock, then by event id"
            .x(() => ordered.Select(k => k.EventId).ShouldBe(["e4", "e2", "e3", "e1"]));
    }

    [Scenario]
    public void The_device_does_not_decide_the_order_of_events_with_equal_clocks()
    {
        TreeEvent fromB = default!;
        TreeEvent fromA = default!;
        List<TreeEvent> ordered = default!;

        "Given an event of device B with event id 'e1' and one of device A with 'e2', both at the same clock"
            .x(() =>
            {
                fromB = new ItemDeleted("e1", DeviceB, new EventClock(7), "action", new ItemId("n"));
                fromA = new ItemDeleted("e2", DeviceA, new EventClock(7), "action", new ItemId("n"));
            });

        "When the events are put in the agreed order"
            .x(() => ordered = new[] { fromA, fromB }.OrderBy(EventKey.Of, EventKey.Comparer).ToList());

        "Then device B's event comes first, because its event id is lower, although device A's id sorts first"
            .x(() => ordered.ShouldBe([fromB, fromA]));
    }
}
