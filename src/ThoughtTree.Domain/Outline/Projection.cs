namespace ThoughtTree.Domain.Outline;

/// <summary>Rebuilds the tree from events: duplicates dropped, one agreed order, last writer wins (ADR-002, ADR-014).</summary>
public static class Projection
{
    public static Tree Replay(IEnumerable<TreeEvent> events)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var ordered = events
            .Where(e => seen.Add(e.EventId)) // INV-2: the same event twice changes nothing
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
                return tree.WithAnomaly(AnomalyKind.DuplicateCreate, e.EventId);
            case ItemCreated c:
                return Place(tree.WithItem(new Item(c.Item, c.Title, c.Note)), c.Item, c.Parent, c.After);
            case ItemTitleChanged t when tree.Items.TryGetValue(t.Item, out var item):
                return tree.WithItem(item.WithTitle(t.Title));
            case ItemNoteChanged n when tree.Items.TryGetValue(n.Item, out var item):
                return tree.WithItem(item.WithNote(n.Note));
            case ItemMoved m when tree.Items.TryGetValue(m.Item, out var item):
                return Place(tree.RemoveChild(item.Parent, m.Item), m.Item, m.Parent, m.After);
            case ItemDeleted d when tree.Items.TryGetValue(d.Item, out var item):
                return tree.WithItem(item.WithDeleted(true));
            case ItemRestored r when tree.Items.TryGetValue(r.Item, out var item):
                return tree.WithItem(item.WithDeleted(false));
            default:
                return tree.WithAnomaly(AnomalyKind.UnknownItem, e.EventId);
        }
    }

    private static Tree Place(Tree tree, ItemId id, ItemId? parent, ItemId? after)
    {
        // ADR-023. The item is already detached from its old parent, so test the cycle through the parent chain.
        if (parent is { } p && (!tree.Items.ContainsKey(p) || tree.IsSameOrDescendant(p, id)))
        {
            parent = null;
        }

        return tree.WithItem(tree.Items[id].WithParent(parent)).InsertChild(parent, id, after);
    }
}
