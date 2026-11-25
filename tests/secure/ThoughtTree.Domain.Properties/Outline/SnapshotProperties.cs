using CsCheck;
using ThoughtTree.Domain.Outline;
using ThoughtTree.Domain.Properties.Generators;

namespace ThoughtTree.Domain.Properties.Outline;

/// <summary>INV-6 (snapshot equivalence) and INV-7 (incremental equals full) over generated histories.</summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-002")]
[Trait("ADR", "ADR-016")]
public class SnapshotProperties
{
    private const int Iterations = 1000;
    private const string Seed = "0N0XIzNsQ0O2";

    private static string State(IEnumerable<TreeEvent> events) => TreeSnapshot.Render(Projection.Replay(events));

    private static List<TreeEvent> Sorted(IEnumerable<TreeEvent> events) =>
        events.DistinctBy(e => e.EventId).OrderBy(EventKey.Of, EventKey.Comparer).ToList();

    [Fact]
    public void INV_7_events_applied_one_at_a_time_in_order_equal_a_full_replay_at_every_step()
    {
        HistoryGen.HistoryAndShuffle.Sample(t =>
        {
            var projector = new Projector();
            var applied = new List<TreeEvent>();
            foreach (TreeEvent e in Sorted(t.Shuffled))
            {
                projector.TryAdd(e).ShouldBeTrue();
                applied.Add(e);
                TreeSnapshot.Render(projector.Tree).ShouldBe(State(applied));
            }
        }, seed: Seed, iter: Iterations, threads: 1);
    }

    [Fact]
    public void INV_7_whatever_the_projector_accepts_from_any_arrival_order_equals_a_full_replay_of_those_events()
    {
        HistoryGen.HistoryAndShuffle.Sample(t =>
        {
            var projector = new Projector();
            var accepted = new List<TreeEvent>();
            foreach (TreeEvent e in t.Shuffled)
            {
                string before = TreeSnapshot.Render(projector.Tree);
                if (projector.TryAdd(e))
                {
                    accepted.Add(e);
                }
                else
                {
                    // A refusal changes nothing; the caller rebuilds from the log.
                    TreeSnapshot.Render(projector.Tree).ShouldBe(before);
                }

                TreeSnapshot.Render(projector.Tree).ShouldBe(State(accepted));
            }

            // The fallback the caller uses on a refusal gives the full answer.
            State(t.Shuffled).ShouldBe(State(t.Events));
        }, seed: Seed, iter: Iterations, threads: 1);
    }

    [Fact]
    public void INV_6_a_snapshot_of_any_prefix_plus_the_remaining_events_equals_a_full_replay()
    {
        Gen.Select(HistoryGen.HistoryAndShuffle, Gen.Int[0, 60], (t, cut) => (t, cut)).Sample(x =>
        {
            List<TreeEvent> sorted = Sorted(x.t.Events);
            int k = sorted.Count == 0 ? 0 : x.cut % (sorted.Count + 1);
            var projector = new Projector();
            foreach (TreeEvent e in sorted.Take(k))
            {
                projector.TryAdd(e);
            }

            Snapshot snapshot = projector.TakeSnapshot();
            var prefix = sorted.Take(k).Select(e => e.EventId).ToHashSet();

            // The events the snapshot lacks arrive in any order, some of them again.
            var remaining = x.t.Shuffled.Where(e => !prefix.Contains(e.EventId)).Concat(x.t.Shuffled.Take(2)).ToList();

            Projector? resumed = Projector.TryResume(snapshot, remaining);
            resumed.ShouldNotBeNull();
            TreeSnapshot.Render(resumed.Tree).ShouldBe(State(x.t.Events));
        }, seed: Seed, iter: Iterations, threads: 1);
    }

    [Fact]
    public void INV_6_a_snapshot_of_any_subset_resumes_to_the_full_replay_or_is_refused_as_stale()
    {
        int staleCount = 0, freshCount = 0;
        Gen.Select(HistoryGen.HistoryAndShuffle, Gen.Bool.List[1, 60], Gen.Int[0, 60], (t, mask, cut) => (t, mask, cut)).Sample(x =>
        {
            List<TreeEvent> sorted = Sorted(x.t.Events);
            // Half of the cases are a prefix of the order (never stale), half an arbitrary subset (usually stale).
            var inSnapshot = x.cut % 2 == 0
                ? sorted.Take(x.cut % (sorted.Count + 1)).ToList()
                : sorted.Where((_, i) => x.mask[i % x.mask.Count]).ToList();
            var projector = new Projector();
            foreach (TreeEvent e in inSnapshot)
            {
                projector.TryAdd(e);
            }

            var ids = inSnapshot.Select(e => e.EventId).ToHashSet();
            var remaining = x.t.Shuffled.Where(e => !ids.Contains(e.EventId)).ToList();
            Projector? resumed = Projector.TryResume(projector.TakeSnapshot(), remaining);

            bool stale = inSnapshot.Count > 0 && remaining.Any(e =>
                EventKey.Comparer.Compare(EventKey.Of(e), EventKey.Of(inSnapshot[^1])) < 0);

            if (stale)
            {
                staleCount++;
                resumed.ShouldBeNull();
            }
            else
            {
                freshCount++;
                resumed.ShouldNotBeNull();
                TreeSnapshot.Render(resumed.Tree).ShouldBe(State(x.t.Events));
            }
        }, seed: Seed, iter: Iterations, threads: 1);

        // Both outcomes must be common, or the property proves little.
        staleCount.ShouldBeGreaterThan(Iterations / 10);
        freshCount.ShouldBeGreaterThan(Iterations / 10);
    }
}
