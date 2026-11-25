using CsCheck;
using ThoughtTree.Domain.Properties.Support;
using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Properties.Clock;

/// <summary>INV-8 over generated device histories: wall time jumping back and forth, and clocks received from other devices.</summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-014")]
public class ClockProperties
{
    // Steps: (true, t) the device writes at wall time t; (false, t) it receives an event stamped t.
    private static readonly Gen<(bool Write, long Time)> Step = Gen.Select(
        Gen.Bool,
        Gen.OneOf(Gen.Long[0, 20], Gen.Const(4_000_000_000_000L), Gen.Const(long.MaxValue - 1), Gen.Const(long.MaxValue)));

    [Fact]
    public void INV_8_a_new_event_is_above_every_clock_in_the_log_and_ties_only_at_the_largest_value()
    {
        Step.List[1, 30].SampleFixed(steps =>
        {
            EventClock? highest = null;
            foreach (var (write, time) in steps)
            {
                var stamp = write ? HybridClock.Next(time, highest) : new EventClock(time);
                if (write && highest is { } h)
                {
                    if (h.Value == long.MaxValue)
                    {
                        stamp.ShouldBe(h);
                    }
                    else
                    {
                        stamp.ShouldBeGreaterThan(h);
                    }
                }

                highest = highest is { } current && current >= stamp ? current : stamp;
            }
        });
    }
}
