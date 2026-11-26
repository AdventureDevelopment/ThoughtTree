using System.Collections.Immutable;

namespace ThoughtTree.Domain.Outline;

/// <summary>Applies events one at a time, in any arrival order, as an immutable value that can be kept (ADR-002, INV-6, INV-7).</summary>
public sealed record Projector
{
    private const int DefaultCheckpointInterval = 1000;

    private static readonly IComparer<TreeEvent> AgreedOrder =
        Comparer<TreeEvent>.Create(static (a, b) => EventKey.Comparer.Compare(EventKey.Of(a), EventKey.Of(b)));

    private Projector(int checkpointInterval)
    {
        this.CheckpointInterval = checkpointInterval;
        this.Checkpoints = [new Checkpoint(0, Tree.Empty)];
    }

    public static Projector Empty { get; } = new(DefaultCheckpointInterval);

    public Tree Tree { get; private init; } = Tree.Empty;

    private int CheckpointInterval { get; init; }

    private ImmutableHashSet<EventId> Ids { get; init; } = [];

    private ImmutableList<TreeEvent> AppliedInAgreedOrder { get; init; } = [];

    private ImmutableList<Checkpoint> Checkpoints { get; init; }

    public Projector Add(TreeEvent e) => this.AddRange([e]);

    /// <summary>Rebuilds once, from the earliest new event, however many of the events arrive late.</summary>
    public Projector AddRange(IEnumerable<TreeEvent> events)
    {
        var newEvents = events.DistinctBy(e => e.EventId).Where(e => !this.Ids.Contains(e.EventId)).Order(AgreedOrder).ToList();
        if (newEvents.Count == 0)
        {
            return this;
        }

        var applied = this.AppliedInAgreedOrder;
        foreach (var e in newEvents)
        {
            applied = applied.Insert(~applied.BinarySearch(e, AgreedOrder), e);
        }

        var earliestNew = applied.BinarySearch(newEvents[0], AgreedOrder);
        var allArriveAfterEverythingApplied = earliestNew == this.AppliedInAgreedOrder.Count;
        var withEvents = this with
        {
            Ids = this.Ids.Union(newEvents.Select(e => e.EventId)),
            AppliedInAgreedOrder = applied,
        };

        return allArriveAfterEverythingApplied
            ? withEvents.ApplyFrom(earliestNew, this.Tree)
            : withEvents.RewindTo(this.Checkpoints.FindLast(c => c.Position <= earliestNew)!);
    }

    internal static Projector WithCheckpointInterval(int events) => new(events);

    internal int CheckpointCount => this.Checkpoints.Count;

    private Projector RewindTo(Checkpoint checkpoint) =>
        (this with { Checkpoints = this.Checkpoints.RemoveAll(c => c.Position > checkpoint.Position) })
            .ApplyFrom(checkpoint.Position, checkpoint.Tree);

    private Projector ApplyFrom(int position, Tree tree)
    {
        var checkpoints = this.Checkpoints;
        for (var next = position; next < this.AppliedInAgreedOrder.Count; next++)
        {
            tree = Projection.Apply(tree, this.AppliedInAgreedOrder[next]);
            var applied = next + 1;
            if (applied % this.CheckpointInterval == 0)
            {
                checkpoints = checkpoints.Add(new Checkpoint(applied, tree));
            }
        }

        return this with { Tree = tree, Checkpoints = checkpoints };
    }

    private sealed record Checkpoint(int Position, Tree Tree);
}
