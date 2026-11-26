using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Budgets.Outline;

/// <summary>
/// PF6: ten times more events take about ten times as long, never quadratically more. Only the ratio between 10,000 and
/// 100,000 events and between 1,000 and 10,000 is checked, so the machine's speed does not matter (architecture.md section 8).
/// </summary>
[Trait("Intent", "Secure")]
public sealed class ReplayBudgets
{
    private const double MostGrowthPerTenfold = 25;

    private const int Runs = 3;

    [Fact]
    public void PF6_a_full_replay_grows_about_linearly() =>
        AssertLinear(n =>
        {
            var history = ReferenceHistories.BranchingTree(n);
            return () => Projection.Replay(history);
        });

    [Fact]
    public void PF6_items_added_one_after_another_under_one_parent_grow_about_linearly() =>
        AssertLinear(n =>
        {
            var history = ReferenceHistories.WideParent(n);
            return () => Projection.Replay(history);
        });

    [Fact]
    public void PF6_a_projector_taking_events_one_at_a_time_grows_about_linearly() =>
        AssertLinear(n =>
        {
            var history = ReferenceHistories.BranchingTree(n);
            return () => _ = history.Aggregate(Projector.Empty, (p, e) => p.Add(e));
        });

    [Fact]
    public void PF6_a_sync_batch_of_late_events_grows_about_linearly() =>
        AssertLinear(n =>
        {
            var (applied, late) = ReferenceHistories.SyncAfterOfflineWork(n);
            var before = Projector.Empty.AddRange(applied);
            return () => _ = before.AddRange(late);
        });

    private static void AssertLinear(Func<int, Action> workloadOfSize)
    {
        var small = FastestRun(workloadOfSize(1_000));
        var medium = FastestRun(workloadOfSize(10_000));
        (medium / small).ShouldBeLessThan(MostGrowthPerTenfold, $"1,000 to 10,000 events: {small:F1} ms to {medium:F1} ms");

        var large = FastestRun(workloadOfSize(100_000));
        (large / medium).ShouldBeLessThan(MostGrowthPerTenfold, $"10,000 to 100,000 events: {medium:F1} ms to {large:F1} ms");
    }

    private static double FastestRun(Action workload)
    {
        workload();
        return Enumerable.Range(0, Runs).Min(_ =>
        {
            var start = TimeProvider.System.GetTimestamp();
            workload();
            return TimeProvider.System.GetElapsedTime(start).TotalMilliseconds;
        });
    }
}
