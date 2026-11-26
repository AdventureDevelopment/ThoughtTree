using CsCheck;
using ThoughtTree.Domain.Properties.Support;
using ThoughtTree.Domain.Model;
using ThoughtTree.Domain.Outline;
using ThoughtTree.Domain.Properties.Generators;

namespace ThoughtTree.Domain.Properties.Outline;

/// <summary>
/// Properties of the projection over generated multi-device histories; the inputs are the same in every run (<see cref="Deterministic"/>).
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-002")]
[Trait("ADR", "ADR-004")]
[Trait("ADR", "ADR-005")]
[Trait("ADR", "ADR-014")]
[Trait("ADR", "ADR-023")]
public class OutlineProperties
{
    private static string State(IEnumerable<TreeEvent> events) => CanonicalTree.Render(Projection.Replay(events));

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

            h.Select(e => e.DeviceId).Distinct().Count().ShouldBeLessThanOrEqualTo(3);
            if (h.GroupBy(e => e.Clock).Any(g => g.Select(e => e.DeviceId).Distinct().Count() > 1))
            {
                equalClocksAcrossDevices++;
            }

            if (h.Any(e => e.Clock.Value > 1_000_000_000_000))
            {
                farAhead++;
            }

            if (h.OfType<ItemRestored>().Any(r => h.OfType<ItemDeleted>().Any(d => d.Item == r.Item)))
            {
                deleteThenRestore++;
            }
        }, 500);

        sameClock.ShouldBeGreaterThan(total / 10);
        foreach (var kind in Enum.GetValues<AnomalyKind>())
        {
            reported.GetValueOrDefault(kind).ShouldBeGreaterThan(total / 10, kind.ToString());
        }

        // Each hard case must be common, not a rarity.
        equalClocksAcrossDevices.ShouldBeGreaterThan(total / 4);
        farAhead.ShouldBeGreaterThan(total / 4);
        deleteThenRestore.ShouldBeGreaterThan(total / 4);
    }

    [Fact]
    public void INV_1_order_independence_any_shuffle_gives_the_same_full_state()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(t => State(t.Shuffled).ShouldBe(State(t.Events)));
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
        }).SampleFixed(t => State(t.withDuplicates).ShouldBe(State(t.h)));
    }

    [Fact]
    public void INV_3_merge_is_union_in_any_split_and_grouping()
    {
        // Each event goes to log A, B or C; some go to two logs (overlap, as after a partial sync).
        Gen.Select(HistoryGen.History, Gen.Int[0, 5].List[0, 40], (h, assign) =>
            (h, assign)).SampleFixed(t =>
        {
            TreeEvent[] all = t.h;
            List<TreeEvent> LogOf(int device) => all
                .Where((_, i) => (t.assign.Count == 0 ? 0 : t.assign[i % t.assign.Count]) is var a &&
                    (a % 3 == device || (a >= 3 && (a - 3 + 1) % 3 == device)))
                .ToList();
            List<TreeEvent> a = LogOf(0), b = LogOf(1), c = LogOf(2);
            // Events that landed in no log are put in A so the union is the whole history.
            a.AddRange(all.Except(a.Concat(b).Concat(c)));

            var expected = State(all);
            State(a.Concat(b).Concat(c)).ShouldBe(expected);            // (A + B) + C
            State(a.Concat(b.Concat(c))).ShouldBe(expected);            // A + (B + C)
            State(c.Concat(b).Concat(a)).ShouldBe(expected);            // C + B + A
            State(b.Concat(a).Concat(c).Concat(a)).ShouldBe(expected);  // repeated merge
            // merging two projections' logs: Replay(A+B) is stable under adding C twice
            State(a.Concat(b).Concat(c).Concat(c)).ShouldBe(expected);

            // A device that holds log A receives B and then C one event at a time; a late event forces a rebuild.
            var held = new List<TreeEvent>(a);
            var projector = new Projector();
            foreach (var e in a.OrderBy(EventKey.Of, EventKey.Comparer))
            {
                projector.TryAdd(e).ShouldBeTrue();
            }

            foreach (var e in b.Concat(c))
            {
                held.Add(e);
                if (!projector.TryAdd(e))
                {
                    projector = new Projector();
                    foreach (var x in held.DistinctBy(x => x.EventId).OrderBy(EventKey.Of, EventKey.Comparer))
                    {
                        projector.TryAdd(x).ShouldBeTrue();
                    }
                }
            }

            CanonicalTree.Render(projector.Tree).ShouldBe(expected);
        });
    }

    [Fact]
    public void INV_4_tree_shape_one_parent_no_cycles_every_visible_item_reachable()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(t =>
        {
            Tree tree = Projection.Replay(t.Shuffled);
            var items = tree.AllItems.ToDictionary(i => i.Id);

            // One parent that exists, and the parent chain ends (no cycle).
            foreach (Item item in items.Values)
            {
                var steps = 0;
                for (ItemId? p = item.Parent; p is { } id; p = items[id].Parent)
                {
                    items.ContainsKey(id).ShouldBeTrue();
                    (++steps).ShouldBeLessThanOrEqualTo(items.Count);
                }
            }

            // Every item sits in exactly one sibling list, the one of its parent.
            var listed = new List<ItemId>();
            listed.AddRange(tree.TopLevelOrder);
            foreach (Item item in items.Values)
            {
                listed.AddRange(tree.ChildrenOf(item.Id));
            }

            listed.Count.ShouldBe(items.Count);
            listed.Distinct().Count().ShouldBe(items.Count);
            foreach (ItemId id in tree.TopLevelOrder)
            {
                items[id].Parent.ShouldBeNull();
            }

            foreach (Item item in items.Values)
            {
                foreach (ItemId child in tree.ChildrenOf(item.Id))
                {
                    items[child].Parent.ShouldBe(item.Id);
                }
            }

            // Every visible item (not deleted, no deleted ancestor) is reachable from top level.
            var outline = tree.VisibleOutline().Select(e => e.Item.Id).ToList();
            outline.Distinct().Count().ShouldBe(outline.Count);
            var expectedVisible = items.Values.Where(i => !IsHidden(i)).Select(i => i.Id).ToHashSet();
            outline.ToHashSet().SetEquals(expectedVisible).ShouldBeTrue();

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
            var created = t.Shuffled.OfType<ItemCreated>().Select(c => c.Item).ToHashSet();
            var present = Projection.Replay(t.Shuffled).AllItems.Select(i => i.Id).ToHashSet();
            present.SetEquals(created).ShouldBeTrue();
        });
    }

    /// <summary>
    /// Real projection against the reference model on the same history. The adapter is <see cref="CanonicalTree.Render"/>;
    /// its format is aligned with the model's canonical text.
    /// </summary>
    [Fact]
    public void Projection_equals_the_reference_model_on_every_history()
    {
        HistoryGen.HistoryAndShuffle.SampleFixed(t =>
            State(t.Shuffled).ShouldBe(ReferenceModel.ReplayToCanonicalText(t.Shuffled)));
    }
}
