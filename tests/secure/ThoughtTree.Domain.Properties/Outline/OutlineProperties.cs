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
        HistoryGen.History.SampleFixed(history =>
        {
            total++;
            history.Select(treeEvent => treeEvent.EventId).Distinct().Count().ShouldBe(history.Length);
            var anomalies = Projection.Replay(history).Anomalies;
            if (history.GroupBy(treeEvent => (treeEvent.Clock, treeEvent.DeviceId)).Any(onOneClockAndDevice => onOneClockAndDevice.Count() > 1))
            {
                sameClock++;
            }

            foreach (var kind in anomalies.Select(report => report.Kind).Distinct())
            {
                reported[kind] = reported.GetValueOrDefault(kind) + 1;
            }

            history.Select(treeEvent => treeEvent.DeviceId).Distinct().Count().ShouldBeLessThanOrEqualTo(HistoryGen.Devices.Count);
            if (history.GroupBy(treeEvent => treeEvent.Clock).Any(onOneClock => onOneClock.Select(treeEvent => treeEvent.DeviceId).Distinct().Count() > 1))
            {
                equalClocksAcrossDevices++;
            }

            if (history.Any(treeEvent => treeEvent.Clock.Value > 1_000_000_000_000))
            {
                farAhead++;
            }

            if (history.OfType<ItemRestored>().Any(restored => history.OfType<ItemDeleted>().Any(deleted => deleted.ItemId == restored.ItemId)))
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
        HistoryGen.HistoryAndShuffle.SampleFixed(histories => CanonicalTree.OfReplay(histories.Shuffled).ShouldBe(CanonicalTree.OfReplay(histories.Events)));
    }

    [Fact]
    public void INV_1_anomalies_are_reported_identically_for_every_shuffle()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(histories =>
            Projection.Replay(histories.Shuffled).Anomalies.ShouldBe(Projection.Replay(histories.Events).Anomalies));
    }

    [Fact]
    public void INV_2_duplicates_are_harmless()
    {
        Gen.Select(HistoryGen.History, Gen.Int[0, 7].List[0, 12], (history, picks) =>
        {
            var withDuplicates = history.ToList();
            foreach (var pick in picks)
            {
                TreeEvent duplicate = history[pick % history.Length];
                withDuplicates.Insert((pick * 7) % (withDuplicates.Count + 1), duplicate);
            }

            return (history, withDuplicates);
        }).SampleFixed(histories => CanonicalTree.OfReplay(histories.withDuplicates).ShouldBe(CanonicalTree.OfReplay(histories.history)));
    }

    private static readonly Gen<(TreeEvent[] All, List<TreeEvent> A, List<TreeEvent> B, List<TreeEvent> C)> ThreeLogs =
        Gen.Select(HistoryGen.History, Gen.Int[0, 5].List[0, 40], (all, assign) =>
        {
            var (firstLog, secondLog, thirdLog) = SplitIntoThreeOverlappingLogs(all, assign);
            return (all, firstLog, secondLog, thirdLog);
        });

    [Fact]
    public void INV_3_merge_is_union_in_any_split_and_grouping()
    {
        ThreeLogs.SampleFixed(logs =>
        {
            var expected = CanonicalTree.OfReplay(logs.All);
            CanonicalTree.OfReplay(logs.A.Concat(logs.B).Concat(logs.C)).ShouldBe(expected);
            CanonicalTree.OfReplay(logs.A.Concat(logs.B.Concat(logs.C))).ShouldBe(expected);
            CanonicalTree.OfReplay(logs.C.Concat(logs.B).Concat(logs.A)).ShouldBe(expected);
            CanonicalTree.OfReplay(logs.B.Concat(logs.A).Concat(logs.C).Concat(logs.A)).ShouldBe(expected);
            CanonicalTree.OfReplay(logs.A.Concat(logs.B).Concat(logs.C).Concat(logs.C)).ShouldBe(expected);
        });
    }

    [Fact]
    public void INV_3_a_device_receiving_the_other_logs_event_by_event_ends_with_the_full_tree()
    {
        ThreeLogs.SampleFixed(logs =>
        {
            var projector = logs.A.Concat(logs.B).Concat(logs.C).Aggregate(Projector.Empty, (projectorSoFar, treeEvent) => projectorSoFar.Add(treeEvent));
            CanonicalTree.Render(projector.Tree).ShouldBe(CanonicalTree.OfReplay(logs.All));
        });
    }

    private static (List<TreeEvent> A, List<TreeEvent> B, List<TreeEvent> C) SplitIntoThreeOverlappingLogs(
        TreeEvent[] all, List<int> assign)
    {
        int AssignmentOf(int index) => assign.Count == 0 ? 0 : assign[index % assign.Count];
        bool IsInLog(int assignment, int log) => assignment % 3 == log || (assignment >= 3 && (assignment - 2) % 3 == log);
        List<TreeEvent> LogOf(int log) => [.. all.Where((_, index) => IsInLog(AssignmentOf(index), log))];

        List<TreeEvent> firstLog = LogOf(0), secondLog = LogOf(1), thirdLog = LogOf(2);
        var inNoLog = all.Except(firstLog.Concat(secondLog).Concat(thirdLog));
        firstLog.AddRange(inNoLog);
        return (firstLog, secondLog, thirdLog);
    }

    [Fact]
    public void INV_4_every_parent_exists_and_no_parent_chain_is_a_cycle()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(histories =>
        {
            var items = Projection.Replay(histories.Shuffled).AllItems.ToDictionary(item => item.Id);
            foreach (Item item in items.Values)
            {
                var steps = 0;
                for (ItemId? ancestor = item.Parent; ancestor is { } id; ancestor = items[id].Parent)
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
        HistoryGen.HistoryAndShuffle.SampleFixed(histories =>
        {
            Tree tree = Projection.Replay(histories.Shuffled);
            var items = tree.AllItems.ToDictionary(item => item.Id);
            List<ItemId> listed = [.. tree.TopLevelOrder, .. items.Values.SelectMany(item => tree.ChildrenOf(item.Id))];

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
        HistoryGen.HistoryAndShuffle.SampleFixed(histories =>
        {
            Tree tree = Projection.Replay(histories.Shuffled);
            var items = tree.AllItems.ToDictionary(item => item.Id);
            var outline = tree.VisibleOutline().Select(row => row.Item.Id).ToList();

            outline.Distinct().Count().ShouldBe(outline.Count);
            outline.ToHashSet().SetEquals(items.Values.Where(item => !IsHidden(item)).Select(item => item.Id)).ShouldBeTrue();

            bool IsHidden(Item item)
            {
                for (Item? ancestor = item; ancestor is not null; ancestor = ancestor.Parent is { } parent ? items[parent] : null)
                {
                    if (ancestor.Deleted)
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
        HistoryGen.HistoryAndShuffle.SampleFixed(histories =>
        {
            var created = histories.Shuffled.OfType<ItemCreated>().Select(createdEvent => createdEvent.ItemId).ToHashSet();
            var present = Projection.Replay(histories.Shuffled).AllItems.Select(item => item.Id).ToHashSet();
            present.SetEquals(created).ShouldBeTrue();
        });
    }

    [Fact]
    public void Projection_equals_the_reference_model_on_every_history()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(histories =>
            CanonicalTree.OfReplay(histories.Shuffled).ShouldBe(ReferenceModel.ReplayToCanonicalText(histories.Shuffled)));
    }
}
