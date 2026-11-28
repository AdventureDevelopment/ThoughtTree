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

        var tree = Tree.Empty.ToBuilder();
        foreach (var treeEvent in ordered)
        {
            Apply(tree, treeEvent);
        }

        return tree.ToImmutable();
    }

    internal static void Apply(TreeBuilder tree, TreeEvent treeEvent)
    {
        if (treeEvent is ItemEvent { ItemId: var id } and not ItemCreated && !tree.Contains(id))
        {
            tree.AddAnomaly(AnomalyKind.UnknownItem, treeEvent, id);
            return;
        }

        switch (treeEvent)
        {
            case ItemCreated created when tree.Contains(created.ItemId):
                tree.AddAnomaly(AnomalyKind.DuplicateCreate, treeEvent, created.ItemId);
                break;
            case ItemCreated created:
                CreateWithFallbackPlace(tree, created);
                break;
            case ItemTitleChanged { Title: { } title } titleChanged:
                tree.SetItem(tree[titleChanged.ItemId].WithTitle(title));
                break;
            case ItemTitleChanged:
                break;
            case ItemNoteChanged { Note: { } note } noteChanged:
                tree.SetItem(tree[noteChanged.ItemId].WithNote(note));
                break;
            case ItemNoteChanged:
                break;
            case ItemMoved moved:
                MoveUnlessImpossible(tree, moved);
                break;
            case ItemDeleted deleted:
                tree.SetItem(tree[deleted.ItemId].WithDeleted(true));
                break;
            case ItemRestored restored:
                tree.SetItem(tree[restored.ItemId].WithDeleted(false));
                break;
            default:
                tree.AddAnomaly(AnomalyKind.UnknownEventType, treeEvent, null);
                break;
        }
    }

    private static void CreateWithFallbackPlace(TreeBuilder tree, ItemCreated created)
    {
        var parent = created.ParentId;
        if (tree.IsUnknownParent(parent))
        {
            tree.AddAnomaly(AnomalyKind.UnknownParent, created, created.ItemId);
            parent = null;
        }

        var after = created.AfterItemId;
        if (tree.IsUnusableAfter(after, created.ItemId))
        {
            tree.AddAnomaly(AnomalyKind.UnknownAfter, created, created.ItemId);
            after = null;
        }

        tree.SetItem(new Item(created.ItemId, created.Title ?? default, created.Note ?? default).WithParent(parent));
        tree.InsertChild(parent, created.ItemId, after);
    }

    private static void MoveUnlessImpossible(TreeBuilder tree, ItemMoved moved)
    {
        if (tree.IsUnknownParent(moved.ParentId))
        {
            tree.AddAnomaly(AnomalyKind.UnknownParent, moved, moved.ItemId);
            return;
        }

        if (tree.IsUnusableAfter(moved.AfterItemId, moved.ItemId))
        {
            tree.AddAnomaly(AnomalyKind.UnknownAfter, moved, moved.ItemId);
            return;
        }

        if (moved.ParentId is { } target && tree.IsSameOrDescendant(target, moved.ItemId))
        {
            tree.AddAnomaly(AnomalyKind.Cycle, moved, moved.ItemId);
            return;
        }

        var item = tree[moved.ItemId].WithParent(moved.ParentId);
        tree.RemoveChild(moved.ItemId);
        tree.SetItem(item);
        tree.InsertChild(moved.ParentId, moved.ItemId, moved.AfterItemId);
    }
}
