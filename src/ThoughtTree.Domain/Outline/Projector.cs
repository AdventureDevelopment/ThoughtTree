namespace ThoughtTree.Domain.Outline;

/// <summary>
/// The tree as of a point in the replay order, plus what is needed to carry on from there. A local cache that can be
/// thrown away and rebuilt from the log (ADR-002, ADR-016).
/// </summary>
public sealed class Snapshot
{
    internal Snapshot(Tree tree, EventKey? last, HashSet<string> ids)
    {
        this.Tree = tree;
        this.Last = last;
        this.Ids = ids;
    }

    internal Tree Tree { get; }
    internal EventKey? Last { get; }
    internal HashSet<string> Ids { get; }
}

/// <summary>
/// Applies events one at a time. An event that sorts after everything applied so far is applied directly; one that sorts
/// before it cannot be, and the caller rebuilds from the log with <see cref="Projection.Replay"/> (INV-6, INV-7).
/// </summary>
public sealed class Projector
{
    private readonly HashSet<string> ids = new(StringComparer.Ordinal);
    private EventKey? last;
    private Tree tree = new();

    public Tree Tree => this.tree;

    /// <returns>
    /// True if the event is now part of the tree (or was already). False if it belongs before the events already
    /// applied: nothing changed, and the caller must rebuild from the log.
    /// </returns>
    public bool TryAdd(TreeEvent e)
    {
        if (this.ids.Contains(e.EventId))
        {
            return true; // INV-2
        }

        var key = EventKey.Of(e);
        if (this.last is { } last && EventKey.Comparer.Compare(key, last) < 0)
        {
            return false;
        }

        this.ids.Add(e.EventId);
        this.last = key;
        Projection.Apply(this.tree, e);
        this.tree.SortAnomalies();
        return true;
    }

    /// <summary>A copy of the current state; later events do not change it.</summary>
    public Snapshot TakeSnapshot() => new(this.tree.Clone(), this.last, [.. this.ids]);

    /// <summary>
    /// Carries on from a snapshot with the events it does not contain. Returns null if the snapshot is stale: an unseen
    /// event belongs before its position, so the caller rebuilds from the full log.
    /// </summary>
    public static Projector? TryResume(Snapshot snapshot, IEnumerable<TreeEvent> remaining)
    {
        var projector = new Projector { tree = snapshot.Tree.Clone(), last = snapshot.Last };
        projector.ids.UnionWith(snapshot.Ids);

        foreach (TreeEvent e in remaining.OrderBy(EventKey.Of, EventKey.Comparer))
        {
            if (!projector.TryAdd(e))
            {
                return null;
            }
        }

        return projector;
    }
}
