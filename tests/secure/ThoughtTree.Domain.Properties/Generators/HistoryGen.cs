using CsCheck;
using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Properties.Generators;

/// <summary>Multi-device histories with counted ids, biased towards hard cases and including bad data (ADR-034).</summary>
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

    private static readonly long[] Times = [1, 1, 2, 2, 3, 3, 4, 10, 4_000_000_000_000, long.MaxValue - 1];
    private const int NeverCreated = ItemPool;
    private static readonly int[] Afters = [-1, -1, 0, 0, 0, 1, 2, 3, 4, 5, NeverCreated];
    private static readonly string[] RawTexts = ["", " Cafe\u0301", " one\r\ntwo", " one\n\rtwo\u2028three", " a\u000Bb\u000Cc\u0085d\u2029e", " \u00C5\u212B", " x\uD800y\uDC00\uD83D\uDC4D"];
    private static readonly int[] Parents = [-1, -1, 0, 0, 1, 1, 2, 3, 4, 5, NeverCreated];

    private const int ScrambleStep = 7919;
    private const int ScrambleModulus = 10007;

    private enum Kind
    {
        Create,
        Title,
        Note,
        Move,
        Delete,
        Restore,
        DeleteThenRestore,
        UnknownType,
        MissingText,
    }

    private sealed record Draft(Kind Kind, int Item, int Parent, int After, int Device, long Time, int Text);

    private static readonly Gen<(Kind, int, int, int)> What = Gen.Select(
        Gen.OneOf(
            Kind.Title, Kind.Note, Kind.Move, Kind.Move, Kind.Move, Kind.Move, Kind.Move, Kind.Move, Kind.Move,
            Kind.Delete, Kind.Delete, Kind.Restore, Kind.DeleteThenRestore, Kind.DeleteThenRestore, Kind.UnknownType,
            Kind.MissingText),
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
        (p, a, n) => new Draft(Kind.Create, item, p, a, n.Item1, n.Item2, n.Item3));

    /// <summary>A history in creation order as a device would have logged it (not shuffled).</summary>
    public static Gen<TreeEvent[]> History { get; } =
        Gen.Select(
            Gen.Int[2, ItemPool],
            Gen.Select(CreateDraft(0), CreateDraft(1), CreateDraft(2), CreateDraft(3), CreateDraft(4), CreateDraft(5),
                (a, b, c, d, e, f) => new[] { a, b, c, d, e, f }),
            Gen.Int[0, ItemPool - 1].SelectMany(CreateDraft).List[0, 2],
            DraftGen.List[0, 14],
            (n, creates, duplicateCreates, others) => Build(creates.Take(n).Concat(duplicateCreates).Concat(others).ToList()));

    /// <summary>The history in one random order (INV-1).</summary>
    public static Gen<TreeEvent[]> Shuffled(TreeEvent[] events) => Gen.Shuffle(events);

    public static Gen<(TreeEvent[] Events, TreeEvent[] Shuffled)> HistoryAndShuffle { get; } =
        History.SelectMany(h => Gen.Shuffle(h), (h, s) => (h, s));

    private static TreeEvent[] Build(List<Draft> drafts)
    {
        var result = new List<TreeEvent>();

        EventId EventId() => new($"e0000000-0000-0000-0000-{result.Count * ScrambleStep % ScrambleModulus:D12}");
        ActionId Action() => new($"c0000000-0000-0000-0000-{result.Count:D12}");

        foreach (Draft d in drafts)
        {
            var device = new DeviceId(Devices[d.Device]);
            var clock = new EventClock(d.Time);
            ItemId item = Item(d.Item);
            var title = $"t{d.Text}{RawTexts[d.Text % RawTexts.Length]}";
            var note = $"n{d.Text}{RawTexts[(d.Text + 1) % RawTexts.Length]}";
            switch (d.Kind)
            {
                case Kind.Create:
                    result.Add(new ItemCreated(EventId(), device, clock, Action(), item, Ref(d.Parent), Ref(d.After),
                        d.Text == 3 ? (Title?)null : title, d.Text == 3 ? (Note?)null : note));
                    break;
                case Kind.Title:
                    result.Add(new ItemTitleChanged(EventId(), device, clock, Action(), item, title));
                    break;
                case Kind.Note:
                    result.Add(new ItemNoteChanged(EventId(), device, clock, Action(), item, note));
                    break;
                case Kind.Move:
                    result.Add(new ItemMoved(EventId(), device, clock, Action(), item, Ref(d.Parent), Ref(d.After)));
                    break;
                case Kind.Delete:
                    result.Add(new ItemDeleted(EventId(), device, clock, Action(), item));
                    break;
                case Kind.UnknownType:
                    result.Add(new FutureEvent(EventId(), device, clock, Action()));
                    break;
                case Kind.MissingText when d.Text % 2 == 0:
                    result.Add(new ItemTitleChanged(EventId(), device, clock, Action(), item, null!));
                    break;
                case Kind.MissingText:
                    result.Add(new ItemNoteChanged(EventId(), device, clock, Action(), item, null!));
                    break;
                case Kind.Restore:
                    result.Add(new ItemRestored(EventId(), device, clock, Action(), item));
                    break;
                case Kind.DeleteThenRestore:
                    result.Add(new ItemDeleted(EventId(), device, clock, Action(), item));
                    result.Add(new ItemRestored(EventId(), device, new EventClock(clock.Value + 1), Action(), item));
                    break;
            }
        }

        return [.. result];
    }

    /// <summary>An event type this app does not know, as from a newer app version (ADR-016).</summary>
    public sealed record FutureEvent(EventId EventId, DeviceId DeviceId, EventClock Clock, ActionId ActionId)
        : TreeEvent(EventId, DeviceId, Clock, ActionId);
}
