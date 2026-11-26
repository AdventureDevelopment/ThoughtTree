using CsCheck;
using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Properties.Generators;

/// <summary>
/// Valid multi-device histories, biased towards hard cases: equal clocks across devices, clocks far ahead,
/// the same afterItemId, moves into deleted subtrees or into the item's own subtree (cycles), deletes followed
/// by restores. Ids come from small pools and are counted, never random (ADR-034). Clocks may tie, also within one
/// device; every EventId is unique in the history and does not follow the writing order, so ties apply in varied orders.
/// </summary>
public static class HistoryGen
{
    public static readonly string[] Devices =
    [
        "a0000000-0000-0000-0000-000000000001",
        "a0000000-0000-0000-0000-000000000002",
        "a0000000-0000-0000-0000-000000000003",
    ];

    public const int ItemPool = 6;

    public static ItemId Item(int index) => new($"b0000000-0000-0000-0000-{index:D12}");

    private static ItemId? Ref(int index) => index < 0 ? null : Item(index);

    // Small pools make collisions common; a few far-ahead times exercise the order at the extremes.
    private static readonly long[] Times = [1, 1, 2, 2, 3, 3, 4, 10, 4_000_000_000_000, long.MaxValue - 1];
    // Item 0 appears often as the shared afterItemId and as the shared parent; 6 is never created (unknown id).
    private static readonly int[] Afters = [-1, -1, 0, 0, 0, 1, 2, 3, 4, 5, 6];
    private static readonly int[] Parents = [-1, -1, 0, 0, 1, 1, 2, 3, 4, 5, 6];

    private sealed record Draft(int Kind, int Item, int Parent, int After, int Device, long Time, int Text);

    private static readonly Gen<(int, int, int, int)> What = Gen.Select(
        // kinds: 0 title, 1 note, 2-4 move, 5-6 delete, 7 restore, 8 delete then restore, 9 unknown event type,
        // 10 title or note change without a value
        Gen.OneOf(0, 1, 2, 2, 2, 3, 3, 4, 4, 5, 6, 7, 8, 8, 9, 10),
        Gen.Int[0, ItemPool - 1],
        Gen.OneOf(Parents),
        Gen.OneOf(Afters));

    private static readonly Gen<(int, long, int)> When = Gen.Select(
        Gen.Int[0, Devices.Length - 1],
        Gen.OneOf(Times),
        Gen.Int[0, 3]);

    private static readonly Gen<Draft> DraftGen = Gen.Select(What, When,
        (w, n) => new Draft(w.Item1, w.Item2, w.Item3, w.Item4, n.Item1, n.Item2, n.Item3));

    private static Gen<Draft> CreateDraft(int item) => Gen.Select(Gen.OneOf(Parents), Gen.OneOf(Afters), When,
        (p, a, n) => new Draft(-1, item, p, a, n.Item1, n.Item2, n.Item3));

    /// <summary>A history in creation order as a device would have logged it (not shuffled).</summary>
    public static Gen<TreeEvent[]> History { get; } =
        Gen.Select(
            Gen.Int[2, ItemPool],
            Gen.Select(CreateDraft(0), CreateDraft(1), CreateDraft(2), CreateDraft(3), CreateDraft(4), CreateDraft(5),
                (a, b, c, d, e, f) => new[] { a, b, c, d, e, f }),
            Gen.Int[0, ItemPool - 1].SelectMany(CreateDraft).List[0, 2], // repeated creates of an existing id
            DraftGen.List[0, 14],
            (n, creates, repeats, others) => Build(creates.Take(n).Concat(repeats).Concat(others).ToList()));

    /// <summary>The history in one random order (INV-1 shuffles).</summary>
    public static Gen<TreeEvent[]> Shuffled(TreeEvent[] events) => Gen.Shuffle(events);

    public static Gen<(TreeEvent[] Events, TreeEvent[] Shuffled)> HistoryAndShuffle { get; } =
        History.SelectMany(h => Gen.Shuffle(h), (h, s) => (h, s));

    /// <summary>Turns drafts into events: counted ids.</summary>
    private static TreeEvent[] Build(List<Draft> drafts)
    {
        var result = new List<TreeEvent>();

        // Scrambled, still unique (7919 and 10007 are prime, histories stay far below 10007 events).
        string EventId() => $"e0000000-0000-0000-0000-{result.Count * 7919 % 10007:D12}";
        string Action() => $"c0000000-0000-0000-0000-{result.Count:D12}";

        foreach (Draft d in drafts)
        {
            var device = Devices[d.Device];
            var clock = new EventClock(d.Time);
            ItemId item = Item(d.Item);
            var title = $"t{d.Text}";
            switch (d.Kind)
            {
                case -1:
                    result.Add(new ItemCreated(EventId(), device, clock, Action(), item, Ref(d.Parent), Ref(d.After),
                        d.Text == 3 ? null! : title, d.Text == 3 ? null! : $"n{d.Text}"));
                    break;
                case 0:
                    result.Add(new ItemTitleChanged(EventId(), device, clock, Action(), item, title));
                    break;
                case 1:
                    result.Add(new ItemNoteChanged(EventId(), device, clock, Action(), item, $"n{d.Text}"));
                    break;
                case 2 or 3 or 4:
                    result.Add(new ItemMoved(EventId(), device, clock, Action(), item, Ref(d.Parent), Ref(d.After)));
                    break;
                case 5 or 6:
                    result.Add(new ItemDeleted(EventId(), device, clock, Action(), item));
                    break;
                case 9:
                    result.Add(new FutureEvent(EventId(), device, clock, Action()));
                    break;
                case 10 when d.Text % 2 == 0:
                    result.Add(new ItemTitleChanged(EventId(), device, clock, Action(), item, null!));
                    break;
                case 10:
                    result.Add(new ItemNoteChanged(EventId(), device, clock, Action(), item, null!));
                    break;
                case 7:
                    result.Add(new ItemRestored(EventId(), device, clock, Action(), item));
                    break;
                default:
                    result.Add(new ItemDeleted(EventId(), device, clock, Action(), item));
                    result.Add(new ItemRestored(EventId(), device, new EventClock(clock.Value + 1), Action(), item));
                    break;
            }
        }

        return [.. result];
    }

    /// <summary>An event type this app does not know, as from a newer app version (ADR-016).</summary>
    public sealed record FutureEvent(string EventId, string DeviceId, EventClock Clock, string ActionId)
        : TreeEvent(EventId, DeviceId, Clock, ActionId);
}
