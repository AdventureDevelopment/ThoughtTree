namespace ThoughtTree.Domain.Outline;

/// <summary>One item of the projection. Deleted items stay as tombstones with their children (ADR-005).</summary>
public sealed class Item(ItemId id, string title, string note)
{
    public ItemId Id { get; } = id;
    public string Title { get; internal set; } = title;
    public string Note { get; internal set; } = note;
    public bool Deleted { get; internal set; }
    public ItemId? Parent { get; internal set; }
    internal List<ItemId> Children { get; } = [];
}

/// <summary>The tree as a projection of the event log (ADR-002).</summary>
public sealed class Tree
{
    private readonly List<Anomaly> _anomalies = [];

    internal Dictionary<ItemId, Item> Items { get; } = [];
    internal List<ItemId> TopLevel { get; } = [];

    /// <summary>Every item ever created, visible or deleted (INV-5).</summary>
    public IReadOnlyCollection<Item> AllItems => Items.Values;

    /// <summary>Events the user should review: flagged or not applied. Sorted by kind, then event id.</summary>
    public IReadOnlyList<Anomaly> Anomalies => _anomalies;

    /// <summary>Top-level items in order, deleted ones included.</summary>
    public IReadOnlyList<ItemId> TopLevelOrder => TopLevel;

    /// <summary>Children of an item in order, deleted ones included.</summary>
    public IReadOnlyList<ItemId> ChildrenOf(ItemId id) => Items[id].Children;

    internal void Report(AnomalyKind kind, string eventId) => _anomalies.Add(new Anomaly(kind, eventId));

    internal void SortAnomalies() =>
        _anomalies.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : string.CompareOrdinal(a.EventId, b.EventId));

    public Item? Find(ItemId id) => Items.GetValueOrDefault(id);

    /// <summary>Items in outline order that a user sees: not deleted and no deleted ancestor.</summary>
    public IReadOnlyList<(int Depth, Item Item)> VisibleOutline()
    {
        var result = new List<(int, Item)>();
        Walk(TopLevel, 0);
        return result;

        void Walk(List<ItemId> ids, int depth)
        {
            foreach (ItemId id in ids)
            {
                Item item = Items[id];
                if (item.Deleted)
                {
                    continue;
                }

                result.Add((depth, item));
                Walk(item.Children, depth + 1);
            }
        }
    }

    /// <summary>Titles as an indented text outline, for expected-tree comparisons.</summary>
    public string ToOutline() =>
        string.Join('\n', VisibleOutline().Select(e => new string(' ', e.Depth * 2) + "- " + e.Item.Title));

    internal bool IsSameOrDescendant(ItemId candidate, ItemId ancestor)
    {
        for (ItemId? current = candidate; current is { } c; current = Items[c].Parent)
        {
            if (c == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    internal List<ItemId> SiblingsOf(ItemId? parent) => parent is { } p ? Items[p].Children : TopLevel;
}
