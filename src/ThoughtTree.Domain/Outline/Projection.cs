using ThoughtTree.Domain.Clock;

namespace ThoughtTree.Domain.Outline;

/// <summary>Rebuilds the tree from events: duplicates dropped, one agreed order, last writer wins (ADR-002, ADR-014).</summary>
public static class Projection
{
    public static Tree Replay(IEnumerable<TreeEvent> events)
    {
        var tree = new Tree();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // INV-1: events that tie on clock and device (bad data only) fall back to the event id.
        var ordered = events
            .Where(e => seen.Add(e.EventId)) // INV-2: the same event twice changes nothing
            .OrderBy(EventKey.Of, EventKey.Comparer)
            .ToList();

        foreach (TreeEvent e in ordered)
        {
            Apply(tree, e);
        }

        tree.SortAnomalies();
        return tree;
    }

    internal static void Apply(Tree tree, TreeEvent e)
    {
        switch (e)
        {
            case ItemCreated c when tree.Items.ContainsKey(c.Item):
                tree.Report(AnomalyKind.DuplicateCreate, e.EventId);
                break;
            case ItemCreated c:
                tree.Items[c.Item] = new Item(c.Item, c.Title, c.Note);
                Place(tree, c.Item, c.Parent, c.After);
                break;
            case ItemTitleChanged t when tree.Items.TryGetValue(t.Item, out Item? item):
                item.Title = t.Title;
                break;
            case ItemNoteChanged n when tree.Items.TryGetValue(n.Item, out Item? item):
                item.Note = n.Note;
                break;
            case ItemMoved m when tree.Items.ContainsKey(m.Item):
                tree.SiblingsOf(tree.Items[m.Item].Parent).Remove(m.Item);
                Place(tree, m.Item, m.Parent, m.After);
                break;
            case ItemDeleted d when tree.Items.TryGetValue(d.Item, out Item? item):
                item.Deleted = true;
                break;
            case ItemRestored r when tree.Items.TryGetValue(r.Item, out Item? item):
                item.Deleted = false;
                break;
            default:
                tree.Report(AnomalyKind.UnknownItem, e.EventId);
                break;
        }
    }

    private static void Place(Tree tree, ItemId id, ItemId? parent, ItemId? after)
    {
        // ADR-023. The item is already detached from its old parent, so test the cycle through the parent chain.
        if (parent is { } p && (!tree.Items.ContainsKey(p) || tree.IsSameOrDescendant(p, id)))
        {
            parent = null;
        }

        tree.Items[id].Parent = parent;
        List<ItemId> siblings = tree.SiblingsOf(parent);

        // ADR-004: directly after the named sibling; first if it is empty or not a sibling at replay time.
        var index = after is { } a ? siblings.IndexOf(a) + 1 : 0;
        siblings.Insert(index, id);
    }
}
