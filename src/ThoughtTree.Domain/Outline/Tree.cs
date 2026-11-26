using System.Collections;
using System.Collections.Immutable;

namespace ThoughtTree.Domain.Outline;

/// <summary>The tree as a projection of the event log, immutable (ADR-002).</summary>
public sealed record Tree
{
    public static Tree Empty { get; } = new();

    internal ImmutableDictionary<ItemId, Item> Items { get; private init; } = ImmutableDictionary<ItemId, Item>.Empty;

    private ItemId? FirstTopLevel { get; init; }

    private ImmutableDictionary<ItemId, ItemId> FirstChild { get; init; } = ImmutableDictionary<ItemId, ItemId>.Empty;

    private ImmutableDictionary<ItemId, ItemId> NextSibling { get; init; } = ImmutableDictionary<ItemId, ItemId>.Empty;

    private ImmutableDictionary<ItemId, ItemId> PreviousSibling { get; init; } = ImmutableDictionary<ItemId, ItemId>.Empty;

    /// <summary>Grouped by item id, those without an item last; replay order inside a group (model rules 18, 19).</summary>
    public IReadOnlyList<Anomaly> Anomalies =>
        [.. this.Reported.OrderBy(a => a.Item is null).ThenBy(a => a.Item?.Value, StringComparer.Ordinal)];

    /// <summary>In replay order.</summary>
    private ImmutableList<Anomaly> Reported { get; init; } = [];

    /// <summary>Deleted items included (INV-5).</summary>
    public IReadOnlyCollection<Item> AllItems => new ItemCollection(this.Items);

    /// <summary>Deleted items included.</summary>
    public IReadOnlyList<ItemId> TopLevelOrder => this.SiblingsFrom(this.FirstTopLevel);

    /// <summary>Deleted items included.</summary>
    public IReadOnlyList<ItemId> ChildrenOf(ItemId id) => this.SiblingsFrom(this.FirstChild.TryGetValue(id, out var first) ? first : null);

    public Item? Find(ItemId id) => this.Items.GetValueOrDefault(id);

    /// <summary>Items not deleted and without a deleted ancestor, in outline order.</summary>
    public IReadOnlyList<(int Depth, Item Item)> VisibleOutline()
    {
        var result = new List<(int, Item)>();
        Walk(this.TopLevelOrder, 0);
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
                Walk(this.ChildrenOf(id), depth + 1);
            }
        }
    }

    /// <summary>The visible titles as an indented outline.</summary>
    public string ToOutline() =>
        string.Join('\n', this.VisibleOutline().Select(e => new string(' ', e.Depth * 2) + "- " + e.Item.Title));

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
        var previous = this.PreviousSibling.TryGetValue(id, out var p) ? p : (ItemId?)null;
        var next = this.NextSibling.TryGetValue(id, out var n) ? n : (ItemId?)null;
        var tree = this with
        {
            NextSibling = this.NextSibling.Remove(id),
            PreviousSibling = this.PreviousSibling.Remove(id),
        };

        tree = previous is { } before ? tree.LinkAfter(before, next) : tree.WithFirst(parent, next);
        return next is { } following ? tree.LinkBefore(following, previous) : tree;
    }

    internal Tree InsertChild(ItemId? parent, ItemId id, ItemId? after)
    {
        var afterIsASibling = after is { } a && this.Items[a].Parent == parent;
        if (afterIsASibling)
        {
            var before = after!.Value;
            var next = this.NextSibling.TryGetValue(before, out var n) ? n : (ItemId?)null;
            var linked = this.LinkAfter(before, id).LinkBefore(id, before).LinkAfter(id, next);
            return next is { } following ? linked.LinkBefore(following, id) : linked;
        }

        var first = parent is { } p ? (this.FirstChild.TryGetValue(p, out var f) ? f : (ItemId?)null) : this.FirstTopLevel;
        var asFirst = this.WithFirst(parent, id).LinkAfter(id, first);
        return first is { } oldFirst ? asFirst.LinkBefore(oldFirst, id) : asFirst;
    }

    private Tree LinkAfter(ItemId item, ItemId? next) =>
        this with { NextSibling = next is { } n ? this.NextSibling.SetItem(item, n) : this.NextSibling.Remove(item) };

    private Tree LinkBefore(ItemId item, ItemId? previous) =>
        this with { PreviousSibling = previous is { } p ? this.PreviousSibling.SetItem(item, p) : this.PreviousSibling.Remove(item) };

    private Tree WithFirst(ItemId? parent, ItemId? first) => parent switch
    {
        null => this with { FirstTopLevel = first },
        { } p => this with { FirstChild = first is { } f ? this.FirstChild.SetItem(p, f) : this.FirstChild.Remove(p) },
    };

    private List<ItemId> SiblingsFrom(ItemId? first)
    {
        var siblings = new List<ItemId>();
        for (var current = first; current is { } id; current = this.NextSibling.TryGetValue(id, out var n) ? n : null)
        {
            siblings.Add(id);
        }

        return siblings;
    }

    private sealed class ItemCollection(ImmutableDictionary<ItemId, Item> items) : IReadOnlyCollection<Item>
    {
        public int Count => items.Count;

        public IEnumerator<Item> GetEnumerator() => items.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
