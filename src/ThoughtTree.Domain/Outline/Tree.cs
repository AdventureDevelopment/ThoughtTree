using System.Collections;
using System.Collections.Immutable;

namespace ThoughtTree.Domain.Outline;

/// <summary>The tree as a projection of the event log, immutable (ADR-002).</summary>
public sealed record Tree
{
    public static Tree Empty { get; } = new();

    internal ImmutableDictionary<ItemId, Item> Items { get; private init; } = ImmutableDictionary<ItemId, Item>.Empty;

    internal ImmutableList<ItemId> TopLevel { get; private init; } = [];

    /// <summary>Grouped by item id, those without an item last; replay order inside a group (model rules 18, 19).</summary>
    public IReadOnlyList<Anomaly> Anomalies =>
        [.. this.Reported.OrderBy(a => a.Item is null).ThenBy(a => a.Item?.Value, StringComparer.Ordinal)];

    /// <summary>In replay order.</summary>
    private ImmutableList<Anomaly> Reported { get; init; } = [];

    /// <summary>Deleted items included (INV-5).</summary>
    public IReadOnlyCollection<Item> AllItems => new ItemCollection(this.Items);

    /// <summary>Deleted items included.</summary>
    public IReadOnlyList<ItemId> TopLevelOrder => this.TopLevel;

    /// <summary>Deleted items included.</summary>
    public IReadOnlyList<ItemId> ChildrenOf(ItemId id) => this.Items[id].Children;

    public Item? Find(ItemId id) => this.Items.GetValueOrDefault(id);

    /// <summary>Items not deleted and without a deleted ancestor, in outline order.</summary>
    public IReadOnlyList<(int Depth, Item Item)> VisibleOutline()
    {
        var result = new List<(int, Item)>();
        Walk(this.TopLevel, 0);
        return result;

        void Walk(IReadOnlyList<ItemId> ids, int depth)
        {
            foreach (var id in ids)
            {
                var item = this.Items[id];
                if (item.Deleted)
                {
                    continue;
                }

                result.Add((depth, item));
                Walk(item.Children, depth + 1);
            }
        }
    }

    /// <summary>The visible titles as an indented outline.</summary>
    public string ToOutline() =>
        string.Join('\n', this.VisibleOutline().Select(e => new string(' ', e.Depth * 2) + "- " + e.Item.Title));

    // A record prints every property; titles and notes must stay out of logs.
    public override string ToString() => $"Tree with {this.Items.Count} items";

    internal Tree WithItem(Item item) => this with { Items = this.Items.SetItem(item.Id, item) };

    internal Tree WithAnomaly(AnomalyKind kind, TreeEvent e, ItemId? item) =>
        this with { Reported = this.Reported.Add(new Anomaly(kind, e.EventId, item)) };

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

    internal Tree RemoveChild(ItemId? parent, ItemId id)
    {
        if (parent is { } p)
        {
            var holder = this.Items[p];
            return this.WithItem(holder.WithChildren(holder.Children.Remove(id)));
        }

        return this with { TopLevel = this.TopLevel.Remove(id) };
    }

    // ADR-004: IndexOf gives -1 for a non-sibling, so such an item lands first.
    internal Tree InsertChild(ItemId? parent, ItemId id, ItemId? after)
    {
        if (parent is { } p)
        {
            var holder = this.Items[p];
            var index = after is { } a ? holder.Children.IndexOf(a) + 1 : 0;
            return this.WithItem(holder.WithChildren(holder.Children.Insert(index, id)));
        }

        var topIndex = after is { } t ? this.TopLevel.IndexOf(t) + 1 : 0;
        return this with { TopLevel = this.TopLevel.Insert(topIndex, id) };
    }

    private sealed class ItemCollection(ImmutableDictionary<ItemId, Item> items) : IReadOnlyCollection<Item>
    {
        public int Count => items.Count;

        public IEnumerator<Item> GetEnumerator() => items.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
