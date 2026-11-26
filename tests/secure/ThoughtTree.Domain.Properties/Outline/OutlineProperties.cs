using CsCheck;
using ThoughtTree.Domain.Properties.Support;
using ThoughtTree.Domain.Model;
using ThoughtTree.Domain.Outline;
using ThoughtTree.Domain.Properties.Generators;

namespace ThoughtTree.Domain.Properties.Outline;

/// <summary>Properties of the projection over generated multi-device histories.</summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-002")]
[Trait("ADR", "ADR-004")]
[Trait("ADR", "ADR-005")]
[Trait("ADR", "ADR-014")]
[Trait("ADR", "ADR-016")]
[Trait("ADR", "ADR-023")]
public sealed class OutlineProperties
{
    [Fact]
    public void Generated_histories_hit_the_hard_cases()
    {
        var reported = new Dictionary<AnomalyKind, int>();
        int sameClock = 0, equalClocksAcrossDevices = 0, farAhead = 0, deleteThenRestore = 0, total = 0;
        HistoryGen.History.SampleFixed(h =>
        {
            total++;
            h.Select(e => e.EventId).Distinct().Count().ShouldBe(h.Length);
            var anomalies = Projection.Replay(h).Anomalies;
            if (h.GroupBy(e => (e.Clock, e.DeviceId)).Any(g => g.Count() > 1))
            {
                sameClock++;
            }

            foreach (var kind in anomalies.Select(a => a.Kind).Distinct())
            {
                reported[kind] = reported.GetValueOrDefault(kind) + 1;
            }

            h.Select(e => e.DeviceId).Distinct().Count().ShouldBeLessThanOrEqualTo(HistoryGen.Devices.Count);
            if (h.GroupBy(e => e.Clock).Any(g => g.Select(e => e.DeviceId).Distinct().Count() > 1))
            {
                equalClocksAcrossDevices++;
            }

            if (h.Any(e => e.Clock.Value > 1_000_000_000_000))
            {
                farAhead++;
            }

            if (h.OfType<ItemRestored>().Any(r => h.OfType<ItemDeleted>().Any(d => d.ItemId == r.ItemId)))
            {
                deleteThenRestore++;
            }
        }, 500);

        sameClock.ShouldBeGreaterThan(total / 10);
        foreach (var kind in Enum.GetValues<AnomalyKind>())
        {
            reported.GetValueOrDefault(kind).ShouldBeGreaterThan(total / 10, kind.ToString());
        }

        equalClocksAcrossDevices.ShouldBeGreaterThan(total / 4);
        farAhead.ShouldBeGreaterThan(total / 4);
        deleteThenRestore.ShouldBeGreaterThan(total / 4);
    }

    [Fact]
    public void INV_1_order_independence_any_shuffle_gives_the_same_full_state()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(t => CanonicalTree.OfReplay(t.Shuffled).ShouldBe(CanonicalTree.OfReplay(t.Events)));
    }

    [Fact]
    public void INV_1_anomalies_are_reported_identically_for_every_shuffle()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(t =>
            Projection.Replay(t.Shuffled).Anomalies.ShouldBe(Projection.Replay(t.Events).Anomalies));
    }

    [Fact]
    public void INV_2_duplicates_are_harmless()
    {
        Gen.Select(HistoryGen.History, Gen.Int[0, 7].List[0, 12], (h, picks) =>
        {
            var withDuplicates = h.ToList();
            foreach (var p in picks)
            {
                TreeEvent e = h[p % h.Length];
                withDuplicates.Insert((p * 7) % (withDuplicates.Count + 1), e);
            }

            return (h, withDuplicates);
        }).SampleFixed(t => CanonicalTree.OfReplay(t.withDuplicates).ShouldBe(CanonicalTree.OfReplay(t.h)));
    }

    private static readonly Gen<(TreeEvent[] All, List<TreeEvent> A, List<TreeEvent> B, List<TreeEvent> C)> ThreeLogs =
        Gen.Select(HistoryGen.History, Gen.Int[0, 5].List[0, 40], (all, assign) =>
        {
            var (a, b, c) = SplitIntoThreeOverlappingLogs(all, assign);
            return (all, a, b, c);
        });

    [Fact]
    public void INV_3_merge_is_union_in_any_split_and_grouping()
    {
        ThreeLogs.SampleFixed(t =>
        {
            var expected = CanonicalTree.OfReplay(t.All);
            CanonicalTree.OfReplay(t.A.Concat(t.B).Concat(t.C)).ShouldBe(expected);
            CanonicalTree.OfReplay(t.A.Concat(t.B.Concat(t.C))).ShouldBe(expected);
            CanonicalTree.OfReplay(t.C.Concat(t.B).Concat(t.A)).ShouldBe(expected);
            CanonicalTree.OfReplay(t.B.Concat(t.A).Concat(t.C).Concat(t.A)).ShouldBe(expected);
            CanonicalTree.OfReplay(t.A.Concat(t.B).Concat(t.C).Concat(t.C)).ShouldBe(expected);
        });
    }

    [Fact]
    public void INV_3_a_device_receiving_the_other_logs_event_by_event_ends_with_the_full_tree()
    {
        ThreeLogs.SampleFixed(t =>
        {
            var projector = t.A.Concat(t.B).Concat(t.C).Aggregate(Projector.Empty, (p, e) => p.Add(e));
            CanonicalTree.Render(projector.Tree).ShouldBe(CanonicalTree.OfReplay(t.All));
        });
    }

    private static (List<TreeEvent> A, List<TreeEvent> B, List<TreeEvent> C) SplitIntoThreeOverlappingLogs(
        TreeEvent[] all, List<int> assign)
    {
        int AssignmentOf(int index) => assign.Count == 0 ? 0 : assign[index % assign.Count];
        bool IsInLog(int assignment, int log) => assignment % 3 == log || (assignment >= 3 && (assignment - 2) % 3 == log);
        List<TreeEvent> LogOf(int log) => [.. all.Where((_, i) => IsInLog(AssignmentOf(i), log))];

        List<TreeEvent> a = LogOf(0), b = LogOf(1), c = LogOf(2);
        var inNoLog = all.Except(a.Concat(b).Concat(c));
        a.AddRange(inNoLog);
        return (a, b, c);
    }

    [Fact]
    public void INV_4_every_parent_exists_and_no_parent_chain_is_a_cycle()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(t =>
        {
            var items = Projection.Replay(t.Shuffled).AllItems.ToDictionary(i => i.Id);
            foreach (Item item in items.Values)
            {
                var steps = 0;
                for (ItemId? p = item.Parent; p is { } id; p = items[id].Parent)
                {
                    items.ContainsKey(id).ShouldBeTrue();
                    steps++;
                    steps.ShouldBeLessThanOrEqualTo(items.Count);
                }
            }
        });
    }

    [Fact]
    public void INV_4_every_item_is_in_exactly_one_sibling_list_the_one_of_its_parent()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(t =>
        {
            Tree tree = Projection.Replay(t.Shuffled);
            var items = tree.AllItems.ToDictionary(i => i.Id);
            List<ItemId> listed = [.. tree.TopLevelOrder, .. items.Values.SelectMany(i => tree.ChildrenOf(i.Id))];

            listed.Count.ShouldBe(items.Count);
            listed.Distinct().Count().ShouldBe(items.Count);
            tree.TopLevelOrder.ShouldAllBe(id => items[id].Parent == null);
            foreach (Item item in items.Values)
            {
                tree.ChildrenOf(item.Id).ShouldAllBe(child => items[child].Parent == item.Id);
            }
        });
    }

    [Fact]
    public void INV_4_every_visible_item_is_reachable_from_top_level_once()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(t =>
        {
            Tree tree = Projection.Replay(t.Shuffled);
            var items = tree.AllItems.ToDictionary(i => i.Id);
            var outline = tree.VisibleOutline().Select(e => e.Item.Id).ToList();

            outline.Distinct().Count().ShouldBe(outline.Count);
            outline.ToHashSet().SetEquals(items.Values.Where(i => !IsHidden(i)).Select(i => i.Id)).ShouldBeTrue();

            bool IsHidden(Item i)
            {
                for (Item? c = i; c is not null; c = c.Parent is { } p ? items[p] : null)
                {
                    if (c.Deleted)
                    {
                        return true;
                    }
                }

                return false;
            }
        });
    }

    [Fact]
    public void INV_5_nothing_lost_every_created_item_is_in_the_projection()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(t =>
        {
            var created = t.Shuffled.OfType<ItemCreated>().Select(c => c.ItemId).ToHashSet();
            var present = Projection.Replay(t.Shuffled).AllItems.Select(i => i.Id).ToHashSet();
            present.SetEquals(created).ShouldBeTrue();
        });
    }

    [Fact]
    public void Projection_equals_the_reference_model_on_every_history()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(t =>
            CanonicalTree.OfReplay(t.Shuffled).ShouldBe(ReferenceModel.ReplayToCanonicalText(t.Shuffled)));
    }
}
