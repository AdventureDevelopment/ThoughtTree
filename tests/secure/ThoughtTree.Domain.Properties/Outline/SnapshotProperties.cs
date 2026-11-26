using CsCheck;
using ThoughtTree.Domain.Outline;
using ThoughtTree.Domain.Properties.Generators;
using ThoughtTree.Domain.Properties.Support;

namespace ThoughtTree.Domain.Properties.Outline;

/// <summary>INV-6 (a kept projector plus the rest equals a full replay) and INV-7 (incremental equals full) over generated histories.</summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-002")]
public sealed class SnapshotProperties
{
    private static readonly Gen<Projector> EmptyProjectors = Gen.OneOf(
        Gen.Const(Projector.Empty),
        Gen.Int[1, 4].Select(Projector.WithCheckpointInterval));

    private static string State(IEnumerable<TreeEvent> events) => CanonicalTree.Render(Projection.Replay(events));

    [Fact]
    public void INV_7_events_applied_one_at_a_time_in_any_arrival_order_equal_a_full_replay_at_every_step()
    {
        Gen.Select(HistoryGen.HistoryAndShuffle, EmptyProjectors).SampleFixed(x =>
        {
            var projector = x.Item2;
            var arrived = new List<TreeEvent>();
            foreach (var e in x.Item1.Shuffled)
            {
                projector = projector.Add(e);
                arrived.Add(e);
                CanonicalTree.Render(projector.Tree).ShouldBe(State(arrived));
            }
        });
    }

    [Fact]
    public void INV_6_a_projector_kept_after_any_subset_plus_the_rest_in_any_order_equals_a_full_replay()
    {
        Gen.Select(HistoryGen.HistoryAndShuffle, Gen.Bool.List[1, 60], EmptyProjectors).SampleFixed(x =>
        {
            var (history, keptSubset, empty) = x;
            var inKept = history.Shuffled.Where((_, i) => keptSubset[i % keptSubset.Count]).ToList();
            var kept = inKept.Aggregate(empty, (p, e) => p.Add(e));
            var keptBefore = CanonicalTree.Render(kept.Tree);

            var rest = history.Shuffled.Except(inKept).Concat(history.Shuffled.Take(2));
            var resumed = rest.Aggregate(kept, (p, e) => p.Add(e));

            CanonicalTree.Render(resumed.Tree).ShouldBe(State(history.Events));
            CanonicalTree.Render(kept.Tree).ShouldBe(keptBefore);
        });
    }

    [Fact]
    public void A_projector_keeps_one_checkpoint_per_interval_of_applied_events_whatever_the_arrival_order()
    {
        Gen.Select(HistoryGen.HistoryAndShuffle, Gen.Int[1, 4]).SampleFixed(x =>
        {
            var (history, interval) = x;
            var projector = history.Shuffled.Aggregate(Projector.WithCheckpointInterval(interval), (p, e) => p.Add(e));
            var applied = history.Shuffled.DistinctBy(e => e.EventId).Count();

            projector.CheckpointCount.ShouldBe((applied / interval) + 1);
        });
    }
}
