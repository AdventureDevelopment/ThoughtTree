namespace ThoughtTree.Domain.Outline;

/// <summary>Rebuilds the tree from events (ADR-002, ADR-014).</summary>
public static class Projection
{
    public static Tree Replay(IEnumerable<TreeEvent> events)
    {
        var ordered = events
            .DistinctBy(treeEvent => treeEvent.EventId)
            .Order(EventKey.AgreedOrder)
            .ToList();

        var tree = Tree.Empty;
        foreach (var treeEvent in ordered)
        {
            tree = Apply(tree, treeEvent);
        }

        return tree;
    }

    internal static Tree Apply(Tree tree, TreeEvent treeEvent)
    {
        if (treeEvent is ItemEvent { ItemId: var id } and not ItemCreated && !tree.Items.ContainsKey(id))
        {
            return tree.WithAnomaly(AnomalyKind.UnknownItem, treeEvent, id);
        }

        return treeEvent switch
        {
            ItemCreated created when tree.Items.ContainsKey(created.ItemId) =>
                tree.WithAnomaly(AnomalyKind.DuplicateCreate, treeEvent, created.ItemId),
            ItemCreated created => CreateWithFallbackPlace(tree, created),
            ItemTitleChanged titleChanged => titleChanged.Title is { } title
                ? tree.WithItem(tree.Items[titleChanged.ItemId].WithTitle(title))
                : tree,
            ItemNoteChanged noteChanged => noteChanged.Note is { } note
                ? tree.WithItem(tree.Items[noteChanged.ItemId].WithNote(note))
                : tree,
            ItemMoved moved => MoveUnlessImpossible(tree, moved),
            ItemDeleted deleted => tree.WithItem(tree.Items[deleted.ItemId].WithDeleted(true)),
            ItemRestored restored => tree.WithItem(tree.Items[restored.ItemId].WithDeleted(false)),
            _ => tree.WithAnomaly(AnomalyKind.UnknownEventType, treeEvent, null),
        };
    }

    private static Tree CreateWithFallbackPlace(Tree tree, ItemCreated created)
    {
        var parent = created.ParentId;
        if (tree.IsUnknownParent(parent))
        {
            tree = tree.WithAnomaly(AnomalyKind.UnknownParent, created, created.ItemId);
            parent = null;
        }

        var after = created.AfterItemId;
        if (tree.IsUnusableAfter(after, created.ItemId))
        {
            tree = tree.WithAnomaly(AnomalyKind.UnknownAfter, created, created.ItemId);
            after = null;
        }

        tree = tree.WithItem(new Item(created.ItemId, created.Title ?? default, created.Note ?? default).WithParent(parent));
        return tree.InsertChild(parent, created.ItemId, after);
    }

    private static Tree MoveUnlessImpossible(Tree tree, ItemMoved moved)
    {
        if (tree.IsUnknownParent(moved.ParentId))
        {
            return tree.WithAnomaly(AnomalyKind.UnknownParent, moved, moved.ItemId);
        }

        if (tree.IsUnusableAfter(moved.AfterItemId, moved.ItemId))
        {
            return tree.WithAnomaly(AnomalyKind.UnknownAfter, moved, moved.ItemId);
        }

        if (moved.ParentId is { } target && tree.IsSameOrDescendant(target, moved.ItemId))
        {
            return tree.WithAnomaly(AnomalyKind.Cycle, moved, moved.ItemId);
        }

        var item = tree.Items[moved.ItemId].WithParent(moved.ParentId);
        return tree.RemoveChild(moved.ItemId).WithItem(item).InsertChild(moved.ParentId, moved.ItemId, moved.AfterItemId);
    }
}
