using System.Collections;
using System.Collections.Immutable;

namespace ThoughtTree.Domain.Outline;

/// <summary>
/// The tree as a projection of the event log (ADR-002), immutable: applying an event returns a new tree that shares
/// everything unchanged with the old one, so a snapshot is just a reference.
/// </summary>
public sealed record Tree
{
    public static Tree Empty { get; } = new();

    internal ImmutableDictionary<ItemId, Item> Items { get; private init; } = ImmutableDictionary<ItemId, Item>.Empty;

    internal ImmutableList<ItemId> TopLevel { get; private init; } = [];

    /// <summary>
    /// Events not carried out as written, grouped by item in item id order, those without an item last; inside a group
    /// in the agreed order (owner decision 2026-11-26, reference model rules 18 and 19).
    /// </summary>
    public IReadOnlyList<Anomaly> Anomalies =>
        [.. this.Reported.OrderBy(a => a.Item is null).ThenBy(a => a.Item?.Value, StringComparer.Ordinal)];

    /// <summary>In the order the events were applied, which is the agreed order.</summary>
    private ImmutableList<Anomaly> Reported { get; init; } = [];

    /// <summary>Every item ever created, visible or deleted (INV-5).</summary>
    public IReadOnlyCollection<Item> AllItems => new ItemCollection(this.Items);

    /// <summary>Top-level items in order, deleted ones included.</summary>
    public IReadOnlyList<ItemId> TopLevelOrder => this.TopLevel;

    /// <summary>Children of an item in order, deleted ones included.</summary>
    public IReadOnlyList<ItemId> ChildrenOf(ItemId id) => this.Items[id].Children;

    public Item? Find(ItemId id) => this.Items.GetValueOrDefault(id);

    /// <summary>Items in outline order that a user sees: not deleted and no deleted ancestor.</summary>
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

    /// <summary>Titles as an indented text outline, for expected-tree comparisons.</summary>
    public string ToOutline() =>
        string.Join('\n', this.VisibleOutline().Select(e => new string(' ', e.Depth * 2) + "- " + e.Item.Title));

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

    /// <summary>ADR-004: directly after the named sibling; first if none is named or it is not a sibling now.</summary>
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
