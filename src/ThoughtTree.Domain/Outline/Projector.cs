using System.Collections.Immutable;

namespace ThoughtTree.Domain.Outline;

/// <summary>The state of a projector at one point of the replay, a cache that can be thrown away (ADR-002).</summary>
public sealed class Snapshot
{
    internal Snapshot(Tree tree, EventKey? last, ImmutableHashSet<EventId> ids)
    {
        this.Tree = tree;
        this.Last = last;
        this.Ids = ids;
    }

    internal Tree Tree { get; }
    internal EventKey? Last { get; }
    internal ImmutableHashSet<EventId> Ids { get; }
}

/// <summary>Applies events one at a time (INV-6, INV-7).</summary>
public sealed class Projector
{
    private ImmutableHashSet<EventId> ids = [];
    private EventKey? last;
    private Tree tree = Tree.Empty;

    public Tree Tree => this.tree;

    /// <returns>False if the event sorts before those applied; nothing changed then.</returns>
    public bool TryAdd(TreeEvent e)
    {
        var alreadyApplied = this.ids.Contains(e.EventId);
        if (alreadyApplied)
        {
            return true;
        }

        var key = EventKey.Of(e);
        if (this.last is { } last && EventKey.Comparer.Compare(key, last) < 0)
        {
            return false;
        }

        this.ids = this.ids.Add(e.EventId);
        this.last = key;
        this.tree = Projection.Apply(this.tree, e);
        return true;
    }

    public Snapshot TakeSnapshot() => new(this.tree, this.last, this.ids);

    /// <returns>Null if one of the events sorts before the snapshot.</returns>
    public static Projector? TryResume(Snapshot snapshot, IEnumerable<TreeEvent> remaining)
    {
        var projector = new Projector { tree = snapshot.Tree, last = snapshot.Last, ids = snapshot.Ids };

        foreach (var e in remaining.OrderBy(EventKey.Of, EventKey.Comparer))
        {
            if (!projector.TryAdd(e))
            {
                return null;
            }
        }

        return projector;
    }
}
