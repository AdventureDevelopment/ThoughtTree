namespace ThoughtTree.Domain.Outline;

/// <summary>The tree as a projection of the event log (ADR-002).</summary>
public sealed class Tree
{
    private readonly List<Anomaly> anomalies = [];

    internal Dictionary<ItemId, Item> Items { get; } = [];
    internal List<ItemId> TopLevel { get; } = [];

    /// <summary>Every item ever created, visible or deleted (INV-5).</summary>
    public IReadOnlyCollection<Item> AllItems => this.Items.Values;

    /// <summary>Events the user should review: flagged or not applied. Sorted by kind, then event id.</summary>
    public IReadOnlyList<Anomaly> Anomalies => this.anomalies;

    /// <summary>Top-level items in order, deleted ones included.</summary>
    public IReadOnlyList<ItemId> TopLevelOrder => this.TopLevel;

    /// <summary>Children of an item in order, deleted ones included.</summary>
    public IReadOnlyList<ItemId> ChildrenOf(ItemId id) => this.Items[id].Children;

    internal void Report(AnomalyKind kind, string eventId) => this.anomalies.Add(new Anomaly(kind, eventId));

    internal void SortAnomalies() =>
        this.anomalies.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : string.CompareOrdinal(a.EventId, b.EventId));

    /// <summary>A copy that shares the items, which are immutable; used for snapshots.</summary>
    internal Tree Clone()
    {
        var copy = new Tree();
        foreach ((ItemId id, Item item) in this.Items)
        {
            copy.Items[id] = item;
        }

        copy.TopLevel.AddRange(this.TopLevel);
        copy.anomalies.AddRange(this.anomalies);
        return copy;
    }

    public Item? Find(ItemId id) => this.Items.GetValueOrDefault(id);

    /// <summary>Items in outline order that a user sees: not deleted and no deleted ancestor.</summary>
    public IReadOnlyList<(int Depth, Item Item)> VisibleOutline()
    {
        var result = new List<(int, Item)>();
        Walk(this.TopLevel, 0);
        return result;

        void Walk(IReadOnlyList<ItemId> ids, int depth)
        {
            foreach (ItemId id in ids)
            {
                Item item = this.Items[id];
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
        string.Join('\n', this.VisibleOutline().Select(e => new string(' ', e.Depth * 2) + "- " + e.Item.Title));

    internal bool IsSameOrDescendant(ItemId candidate, ItemId ancestor)
    {
        for (ItemId? current = candidate; current is { } c; current = this.Items[c].Parent)
        {
            if (c == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    internal void RemoveChild(ItemId? parent, ItemId id)
    {
        if (parent is { } p)
        {
            var holder = this.Items[p];
            this.Items[p] = holder.WithChildren(holder.Children.Remove(id));
        }
        else
        {
            this.TopLevel.Remove(id);
        }
    }

    /// <summary>ADR-004: directly after the named sibling; first if none is named or it is not a sibling now.</summary>
    internal void InsertChild(ItemId? parent, ItemId id, ItemId? after)
    {
        if (parent is { } p)
        {
            var holder = this.Items[p];
            var index = after is { } a ? holder.Children.IndexOf(a) + 1 : 0;
            this.Items[p] = holder.WithChildren(holder.Children.Insert(index, id));
        }
        else
        {
            var index = after is { } a ? this.TopLevel.IndexOf(a) + 1 : 0;
            this.TopLevel.Insert(index, id);
        }
    }
}
