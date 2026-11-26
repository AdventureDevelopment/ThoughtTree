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
        if (e is ItemEvent { ItemId: var id } and not ItemCreated && !tree.Items.ContainsKey(id))
        {
            return tree.WithAnomaly(AnomalyKind.UnknownItem, e, id);
        }

        return e switch
        {
            ItemCreated c when tree.Items.ContainsKey(c.ItemId) => tree.WithAnomaly(AnomalyKind.DuplicateCreate, e, c.ItemId),
            ItemCreated c => CreateWithFallbackPlace(tree, c),
            ItemTitleChanged t => t.Title is { } title ? tree.WithItem(tree.Items[t.ItemId].WithTitle(title)) : tree,
            ItemNoteChanged n => n.Note is { } note ? tree.WithItem(tree.Items[n.ItemId].WithNote(note)) : tree,
            ItemMoved m => MoveUnlessImpossible(tree, m),
            ItemDeleted d => tree.WithItem(tree.Items[d.ItemId].WithDeleted(true)),
            ItemRestored r => tree.WithItem(tree.Items[r.ItemId].WithDeleted(false)),
            _ => tree.WithAnomaly(AnomalyKind.UnknownEventType, e, null),
        };
    }

    private static Tree CreateWithFallbackPlace(Tree tree, ItemCreated c)
    {
        var parent = c.ParentId;
        if (tree.IsUnknownParent(parent))
        {
            tree = tree.WithAnomaly(AnomalyKind.UnknownParent, c, c.ItemId);
            parent = null;
        }

        var after = c.AfterItemId;
        if (tree.IsUnusableAfter(after, c.ItemId))
        {
            tree = tree.WithAnomaly(AnomalyKind.UnknownAfter, c, c.ItemId);
            after = null;
        }

        tree = tree.WithItem(new Item(c.ItemId, c.Title ?? default, c.Note ?? default).WithParent(parent));
        return tree.InsertChild(parent, c.ItemId, after);
    }

    private static Tree MoveUnlessImpossible(Tree tree, ItemMoved m)
    {
        if (tree.IsUnknownParent(m.ParentId))
        {
            return tree.WithAnomaly(AnomalyKind.UnknownParent, m, m.ItemId);
        }

        if (tree.IsUnusableAfter(m.AfterItemId, m.ItemId))
        {
            return tree.WithAnomaly(AnomalyKind.UnknownAfter, m, m.ItemId);
        }

        if (m.ParentId is { } target && tree.IsSameOrDescendant(target, m.ItemId))
        {
            return tree.WithAnomaly(AnomalyKind.Cycle, m, m.ItemId);
        }

        var moved = tree.Items[m.ItemId].WithParent(m.ParentId);
        return tree.RemoveChild(m.ItemId).WithItem(moved).InsertChild(m.ParentId, m.ItemId, m.AfterItemId);
    }
}
