namespace ThoughtTree.Domain.Outline;

/// <summary>Rebuilds the tree from events (ADR-002, ADR-014).</summary>
public static class Projection
{
    public static Tree Replay(IEnumerable<TreeEvent> events)
    {
        var ordered = events
            .DistinctBy(e => e.EventId)
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
            case ItemCreated c when tree.Items.ContainsKey(c.ItemId):
                return tree.WithAnomaly(AnomalyKind.DuplicateCreate, e, c.ItemId);
            case ItemCreated c:
                return CreateWithFallbackPlace(tree, c);
            case ItemTitleChanged t when tree.Items.TryGetValue(t.ItemId, out var item):
                return t.Title is { } title ? tree.WithItem(item.WithTitle(title)) : tree;
            case ItemNoteChanged n when tree.Items.TryGetValue(n.ItemId, out var item):
                return n.Note is { } note ? tree.WithItem(item.WithNote(note)) : tree;
            case ItemMoved m when tree.Items.ContainsKey(m.ItemId):
                return MoveUnlessImpossible(tree, m);
            case ItemDeleted d when tree.Items.TryGetValue(d.ItemId, out var item):
                return tree.WithItem(item.WithDeleted(true));
            case ItemRestored r when tree.Items.TryGetValue(r.ItemId, out var item):
                return tree.WithItem(item.WithDeleted(false));
            case ItemTitleChanged t:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e, t.ItemId);
            case ItemNoteChanged n:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e, n.ItemId);
            case ItemMoved m:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e, m.ItemId);
            case ItemDeleted d:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e, d.ItemId);
            case ItemRestored r:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e, r.ItemId);
            default:
                return tree.WithAnomaly(AnomalyKind.UnknownEventType, e, null);
        }
    }

    private static Tree CreateWithFallbackPlace(Tree tree, ItemCreated c)
    {
        var parent = c.ParentId;
        if (parent is { } p && !tree.Items.ContainsKey(p))
        {
            tree = tree.WithAnomaly(AnomalyKind.UnknownParent, c, c.ItemId);
            parent = null;
        }

        var after = c.AfterItemId;
        if (after is { } a && (a == c.ItemId || !tree.Items.ContainsKey(a)))
        {
            tree = tree.WithAnomaly(AnomalyKind.UnknownAfter, c, c.ItemId);
            after = null;
        }

        tree = tree.WithItem(new Item(c.ItemId, c.Title ?? default, c.Note ?? default).WithParent(parent));
        return tree.InsertChild(parent, c.ItemId, after);
    }

    private static Tree MoveUnlessImpossible(Tree tree, ItemMoved m)
    {
        if (m.ParentId is { } p && !tree.Items.ContainsKey(p))
        {
            return tree.WithAnomaly(AnomalyKind.UnknownParent, m, m.ItemId);
        }

        if (m.AfterItemId is { } a && (a == m.ItemId || !tree.Items.ContainsKey(a)))
        {
            return tree.WithAnomaly(AnomalyKind.UnknownAfter, m, m.ItemId);
        }

        if (m.ParentId is { } target && tree.IsSameOrDescendant(target, m.ItemId))
        {
            return tree.WithAnomaly(AnomalyKind.Cycle, m, m.ItemId);
        }

        var item = tree.Items[m.ItemId];
        return tree.RemoveChild(item.Parent, m.ItemId).WithItem(item.WithParent(m.ParentId)).InsertChild(m.ParentId, m.ItemId, m.AfterItemId);
    }
}
