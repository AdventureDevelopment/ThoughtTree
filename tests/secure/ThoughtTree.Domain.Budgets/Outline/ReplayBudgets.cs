using System.Collections.Immutable;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Budgets.Outline;

/// <summary>
/// PF6: ten times more events grow the time about as much as ten times more entries grow it for an immutable dictionary,
/// the structure the tree is built on, and never quadratically more. The reference runs alternately with each workload,
/// so the machine's speed, its caches and the depth of the structure cancel out (architecture.md section 8).
/// </summary>
[Trait("Intent", "Secure")]
public sealed class ReplayBudgets
{
    private const double MostGrowthAgainstReference = 3;

    private const int WarmUpRuns = 2;

    private const int Runs = 5;

    [Fact]
    public void PF6_a_full_replay_grows_about_linearly() =>
        AssertLinear(size =>
        {
            var history = ReferenceHistories.BranchingTree(size);
            return () => Projection.Replay(history);
        });

    [Fact]
    public void PF6_items_added_one_after_another_under_one_parent_grow_about_linearly() =>
        AssertLinear(size =>
        {
            var history = ReferenceHistories.WideParent(size);
            return () => Projection.Replay(history);
        });

    [Fact]
    public void PF6_a_projector_taking_events_one_at_a_time_grows_about_linearly() =>
        AssertLinear(size =>
        {
            var history = ReferenceHistories.BranchingTree(size);
            return () => _ = history.Aggregate(Projector.Empty, (projectorSoFar, treeEvent) => projectorSoFar.Add(treeEvent));
        });

    [Fact]
    public void PF6_a_sync_batch_of_late_events_grows_about_linearly() =>
        AssertLinear(size =>
        {
            var (applied, late) = ReferenceHistories.SyncAfterOfflineWork(size);
            var before = Projector.Empty.AddRange(applied);
            return () => _ = before.AddRange(late);
        });

    private static void AssertLinear(Func<int, Action> workloadOfSize)
    {
        var small = Measure(1_000, workloadOfSize);
        var medium = Measure(10_000, workloadOfSize);
        AssertGrowth(small, medium, "1,000 to 10,000 events");

        var large = Measure(100_000, workloadOfSize);
        AssertGrowth(medium, large, "10,000 to 100,000 events");
    }

    private static void AssertGrowth(Timing smaller, Timing larger, string sizes)
    {
        var growth = larger.Workload / smaller.Workload;
        var referenceGrowth = larger.Reference / smaller.Reference;
        (growth / referenceGrowth).ShouldBeLessThan(
            MostGrowthAgainstReference,
            $"{sizes}: {smaller.Workload:F1} ms to {larger.Workload:F1} ms ({growth:F1} times); reference {smaller.Reference:F1} ms to {larger.Reference:F1} ms ({referenceGrowth:F1} times)");
    }

    private static Timing Measure(int size, Func<int, Action> workloadOfSize)
    {
        var workload = workloadOfSize(size);
        var reference = DictionaryOfItemIds(size);
        for (var run = 0; run < WarmUpRuns; run++)
        {
            workload();
            reference();
        }

        var fastest = new Timing(double.MaxValue, double.MaxValue);
        for (var run = 0; run < Runs; run++)
        {
            fastest = new Timing(Math.Min(fastest.Workload, Time(workload)), Math.Min(fastest.Reference, Time(reference)));
        }

        return fastest;
    }

    private static Action DictionaryOfItemIds(int size)
    {
        var ids = Enumerable.Range(0, size).Select(index => new ItemId($"c0000000-0000-0000-0000-{index:D12}")).ToList();
        return () => _ = ids.Aggregate(ImmutableDictionary<ItemId, int>.Empty, (dictionary, id) => dictionary.SetItem(id, 0));
    }

    private static double Time(Action workload)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var start = TimeProvider.System.GetTimestamp();
        workload();
        return TimeProvider.System.GetElapsedTime(start).TotalMilliseconds;
    }

    private sealed record Timing(double Workload, double Reference);
}
