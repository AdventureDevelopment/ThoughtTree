using LambdaTale;
using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Clock;

[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-014")]
public class ClockEdgeSpecs
{
    [Scenario]
    public void INV_8_At_the_largest_clock_a_new_event_takes_that_clock_instead_of_failing()
    {
        EventClock stamp = default!;

        "When a device writes while its log holds an event with the largest possible clock"
            .x(() => stamp = HybridClock.Next(now: 100, highestInLog: new EventClock(long.MaxValue)));

        "Then the new event takes the largest clock, and its event id orders it"
            .x(() => stamp.ShouldBe(new EventClock(long.MaxValue)));
    }

    [Scenario]
    public void Event_ids_are_compared_as_text_ordinal()
    {
        var clock = new EventClock(1);
        static EventKey Key(EventClock c, string id) => new(c, id);

        "Then a digit sorts before a letter, as in ordinal order"
            .x(() => EventKey.Comparer.Compare(
                Key(clock, "9f000000-0000-0000-0000-000000000001"),
                Key(clock, "a0000000-0000-0000-0000-000000000002")).ShouldBeNegative());

        "And a different last character decides between ids that otherwise match"
            .x(() => EventKey.Comparer.Compare(
                Key(clock, "a0000000-0000-0000-0000-00000000000a"),
                Key(clock, "a0000000-0000-0000-0000-00000000000b")).ShouldBeNegative());
    }
}
