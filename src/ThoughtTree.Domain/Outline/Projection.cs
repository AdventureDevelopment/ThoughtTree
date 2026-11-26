namespace ThoughtTree.Domain.Outline;

/// <summary>Rebuilds the tree from events (ADR-002, ADR-014).</summary>
public static class Projection
{
    public static Tree Replay(IEnumerable<TreeEvent> events)
    {
        var ordered = events
            .DistinctBy(e => e.EventId, StringComparer.Ordinal)
            .OrderBy(EventKey.Of, EventKey.Comparer)
            .ToList();

        var tree = Tree.Empty;
        foreach (var e in ordered)
        {
            tree = Apply(tree, e);
        }

        return tree;
    }

    internal static Tree Apply(Tree tree, TreeEvent e)
    {
        switch (e)
        {
            case ItemCreated c when tree.Items.ContainsKey(c.Item):
                return tree.WithAnomaly(AnomalyKind.DuplicateCreate, e, c.Item);
            case ItemCreated c:
                return CreateWithFallbackPlace(tree, c);
            case ItemTitleChanged t when tree.Items.TryGetValue(t.Item, out var item):
                return t.Title is null ? tree : tree.WithItem(item.WithTitle(t.Title));
            case ItemNoteChanged n when tree.Items.TryGetValue(n.Item, out var item):
                return n.Note is null ? tree : tree.WithItem(item.WithNote(n.Note));
            case ItemMoved m when tree.Items.ContainsKey(m.Item):
                return MoveUnlessImpossible(tree, m);
            case ItemDeleted d when tree.Items.TryGetValue(d.Item, out var item):
                return tree.WithItem(item.WithDeleted(true));
            case ItemRestored r when tree.Items.TryGetValue(r.Item, out var item):
                return tree.WithItem(item.WithDeleted(false));
            case ItemTitleChanged t:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e, t.Item);
            case ItemNoteChanged n:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e, n.Item);
            case ItemMoved m:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e, m.Item);
            case ItemDeleted d:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e, d.Item);
            case ItemRestored r:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e, r.Item);
            default:
                return tree.WithAnomaly(AnomalyKind.UnknownEventType, e, null);
        }
    }

    private static Tree CreateWithFallbackPlace(Tree tree, ItemCreated c)
    {
        var parent = c.Parent;
        if (parent is { } p && !tree.Items.ContainsKey(p))
        {
            tree = tree.WithAnomaly(AnomalyKind.UnknownParent, c, c.Item);
            parent = null;
        }

        var after = c.After;
        if (after is { } a && (a == c.Item || !tree.Items.ContainsKey(a)))
        {
            tree = tree.WithAnomaly(AnomalyKind.UnknownAfter, c, c.Item);
            after = null;
        }

        tree = tree.WithItem(new Item(c.Item, c.Title ?? "", c.Note ?? "").WithParent(parent));
        return tree.InsertChild(parent, c.Item, after);
    }

    private static Tree MoveUnlessImpossible(Tree tree, ItemMoved m)
    {
        if (m.Parent is { } p && !tree.Items.ContainsKey(p))
        {
            return tree.WithAnomaly(AnomalyKind.UnknownParent, m, m.Item);
        }

        if (m.After is { } a && (a == m.Item || !tree.Items.ContainsKey(a)))
        {
            return tree.WithAnomaly(AnomalyKind.UnknownAfter, m, m.Item);
        }

        if (m.Parent is { } target && tree.IsSameOrDescendant(target, m.Item))
        {
            return tree.WithAnomaly(AnomalyKind.Cycle, m, m.Item);
        }

        var item = tree.Items[m.Item];
        return tree.RemoveChild(item.Parent, m.Item).WithItem(item.WithParent(m.Parent)).InsertChild(m.Parent, m.Item, m.After);
    }
}
