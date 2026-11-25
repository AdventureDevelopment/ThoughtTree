using LambdaTale;
using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Specs.Clock;

[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-014")]
public class ClockEdgeSpecs
{
    [Scenario]
    public void INV_8_A_full_counter_rolls_into_the_next_millisecond_instead_of_failing()
    {
        EventClock stamp = default!;

        "When a device writes after an event whose counter is at its maximum and whose time is not behind"
            .x(() => stamp = HybridClock.Next(now: 100, highestInLog: new EventClock(100, int.MaxValue)));

        "Then the new clock is above the logged one"
            .x(() =>
            {
                stamp.ShouldBe(new EventClock(101, 0));
                (stamp > new EventClock(100, int.MaxValue)).ShouldBeTrue();
            });
    }

    [Scenario]
    public void Equal_times_are_ordered_by_counter()
    {
        "Then a higher counter is later and a higher time wins over any counter"
            .x(() =>
            {
                (new EventClock(5, 2) > new EventClock(5, 1)).ShouldBeTrue();
                (new EventClock(6, 0) > new EventClock(5, int.MaxValue)).ShouldBeTrue();
            });
    }

    [Scenario]
    public void Device_ids_are_compared_as_lowercase_text_ordinal()
    {
        const string digitFirst = "9f000000-0000-0000-0000-000000000001";
        const string letterFirst = "a0000000-0000-0000-0000-000000000002";
        var clock = new EventClock(1, 0);

        "Then a digit sorts before a letter, as in ordinal order"
            .x(() => EventOrder.Instance.Compare((clock, digitFirst), (clock, letterFirst)).ShouldBeNegative());

        "And a different last byte decides between ids that otherwise match"
            .x(() => EventOrder.Instance.Compare(
                (clock, "a0000000-0000-0000-0000-00000000000a"),
                (clock, "a0000000-0000-0000-0000-00000000000b")).ShouldBeNegative());
    }
}
