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

    [Fact]
    public void INV_7_events_applied_one_at_a_time_in_any_arrival_order_equal_a_full_replay_at_every_step()
    {
        Gen.Select(HistoryGen.HistoryAndShuffle, EmptyProjectors).SampleFixed(sample =>
        {
            var (history, projector) = sample;
            var arrived = new List<TreeEvent>();
            foreach (var treeEvent in history.Shuffled)
            {
                projector = projector.Add(treeEvent);
                arrived.Add(treeEvent);
                CanonicalTree.Render(projector.Tree).ShouldBe(CanonicalTree.OfReplay(arrived));
            }
        });
    }

    [Fact]
    public void INV_6_a_projector_kept_after_any_subset_plus_the_rest_in_any_order_equals_a_full_replay()
    {
        Gen.Select(HistoryGen.HistoryAndShuffle, Gen.Bool.List[1, 60], EmptyProjectors).SampleFixed(sample =>
        {
            var (history, keptSubset, empty) = sample;
            var inKept = history.Shuffled.Where((_, index) => keptSubset[index % keptSubset.Count]).ToList();
            var kept = inKept.Aggregate(empty, (projectorSoFar, treeEvent) => projectorSoFar.Add(treeEvent));
            var keptBefore = CanonicalTree.Render(kept.Tree);

            var rest = history.Shuffled.Except(inKept).Concat(history.Shuffled.Take(2));
            var resumed = rest.Aggregate(kept, (projectorSoFar, treeEvent) => projectorSoFar.Add(treeEvent));

            CanonicalTree.Render(resumed.Tree).ShouldBe(CanonicalTree.OfReplay(history.Events));
            CanonicalTree.Render(kept.Tree).ShouldBe(keptBefore);
        });
    }

    [Fact]
    public void INV_7_events_added_in_batches_of_any_size_and_order_equal_a_full_replay()
    {
        Gen.Select(HistoryGen.HistoryAndShuffle, Gen.Int[1, 8].List[1, 10], EmptyProjectors).SampleFixed(sample =>
        {
            var (history, batchSizes, empty) = sample;
            var projector = empty;
            var remaining = history.Shuffled.AsEnumerable();
            for (var batch = 0; remaining.Any(); batch++)
            {
                var size = batchSizes[batch % batchSizes.Count];
                projector = projector.AddRange(remaining.Take(size));
                remaining = remaining.Skip(size);
            }

            CanonicalTree.Render(projector.Tree).ShouldBe(CanonicalTree.OfReplay(history.Events));
        });
    }

    [Fact]
    public void A_projector_keeps_one_checkpoint_per_interval_of_applied_events_whatever_the_arrival_order()
    {
        Gen.Select(HistoryGen.HistoryAndShuffle, Gen.Int[1, 4]).SampleFixed(sample =>
        {
            var (history, interval) = sample;
            var projector = history.Shuffled.Aggregate(Projector.WithCheckpointInterval(interval), (projectorSoFar, treeEvent) => projectorSoFar.Add(treeEvent));
            var applied = history.Shuffled.DistinctBy(treeEvent => treeEvent.EventId).Count();

            projector.CheckpointCount.ShouldBe((applied / interval) + 1);
        });
    }
}
