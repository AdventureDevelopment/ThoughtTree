using CsCheck;
using ThoughtTree.Domain.Properties.Support;
using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Properties.Clock;

/// <summary>INV-8 over generated device histories: wall time jumping back and forth, and clocks received from other devices.</summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-014")]
public class ClockProperties
{
    private static readonly Gen<(bool DeviceWrites, long WallTimeOrReceivedClock)> Step = Gen.Select(
        Gen.Bool,
        Gen.OneOf(Gen.Long[0, 20], Gen.Const(4_000_000_000_000L), Gen.Const(long.MaxValue - 1), Gen.Const(long.MaxValue)));

    [Fact]
    public void INV_8_a_new_event_gets_exactly_the_ADR_014_clock_above_every_clock_in_the_log()
    {
        Step.List[1, 30].SampleFixed(steps =>
        {
            long? highest = null;
            foreach (var (write, time) in steps)
            {
                var stamp = write ? HybridClock.Next(time, highest is { } h ? new EventClock(h) : null).Value : time;
                if (write)
                {
                    // ADR-014
                    var expected = highest switch
                    {
                        null => time,
                        long.MaxValue => long.MaxValue,
                        { } top => Math.Max(time, top + 1),
                    };
                    stamp.ShouldBe(expected);
                }

                highest = highest is { } current ? Math.Max(current, stamp) : stamp;
            }
        });
    }
}
