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

    /// <summary>Grouped by item id, those without an item last; agreed order inside a group (model rules 18, 19).</summary>
    public IReadOnlyList<Anomaly> Anomalies =>
        [.. this.Reported.OrderBy(report => report.ItemId is null).ThenBy(report => report.ItemId?.Value, StringComparer.Ordinal)];

    /// <summary>In agreed order.</summary>
    private ImmutableList<Anomaly> Reported { get; init; } = [];

    /// <summary>Deleted items included (INV-5).</summary>
    public IReadOnlyCollection<Item> AllItems => new ItemCollection(this.Items);

    /// <summary>Deleted items included.</summary>
    public IReadOnlyList<ItemId> TopLevelOrder => this.SiblingsFrom(this.FirstOf(null));

    /// <summary>Deleted items included.</summary>
    public IReadOnlyList<ItemId> ChildrenOf(ItemId id) => this.SiblingsFrom(this.FirstOf(id));

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
        string.Join('\n', this.VisibleOutline().Select(entry => new string(' ', entry.Depth * 2) + "- " + entry.Item.Title));

    public override string ToString() => $"Tree with {this.Items.Count} items";

    internal Tree WithItem(Item item) => this with { Items = this.Items.SetItem(item.Id, item) };

    internal Tree WithAnomaly(AnomalyKind kind, TreeEvent treeEvent, ItemId? item) =>
        this with { Reported = this.Reported.Add(new Anomaly(kind, treeEvent.EventId, item)) };

    internal bool IsSameOrDescendant(ItemId candidate, ItemId ancestor)
    {
        for (ItemId? current = candidate; current is { } id; current = this.Items[id].Parent)
        {
            if (id == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    internal bool IsUnknownParent(ItemId? parent) => parent is { } parentId && !this.Items.ContainsKey(parentId);

    internal bool IsUnusableAfter(ItemId? after, ItemId item) => after is { } afterId && (afterId == item || !this.Items.ContainsKey(afterId));

    internal Tree RemoveChild(ItemId id)
    {
        var previous = this.PreviousOf(id);
        var next = this.NextOf(id);
        var tree = this with
        {
            NextSibling = this.NextSibling.Remove(id),
            PreviousSibling = this.PreviousSibling.Remove(id),
        };

        tree = previous is { } before ? tree.LinkAfter(before, next) : tree.WithFirst(this.Items[id].Parent, next);
        return next is { } following ? tree.LinkBefore(following, previous) : tree;
    }

    internal Tree InsertChild(ItemId? parent, ItemId id, ItemId? after)
    {
        if (after is { } before && this.Items[before].Parent == parent)
        {
            var next = this.NextOf(before);
            var linked = this.LinkAfter(before, id).LinkBefore(id, before).LinkAfter(id, next);
            return next is { } following ? linked.LinkBefore(following, id) : linked;
        }

        var first = this.FirstOf(parent);
        var asFirst = this.WithFirst(parent, id).LinkAfter(id, first);
        return first is { } oldFirst ? asFirst.LinkBefore(oldFirst, id) : asFirst;
    }

    private ItemId? NextOf(ItemId id) => this.NextSibling.TryGetValue(id, out var next) ? next : null;

    private ItemId? PreviousOf(ItemId id) => this.PreviousSibling.TryGetValue(id, out var previous) ? previous : null;

    private ItemId? FirstOf(ItemId? parent) =>
        parent is { } parentId ? (this.FirstChild.TryGetValue(parentId, out var first) ? first : null) : this.FirstTopLevel;

    private Tree LinkAfter(ItemId item, ItemId? next) =>
        this with { NextSibling = next is { } nextId ? this.NextSibling.SetItem(item, nextId) : this.NextSibling.Remove(item) };

    private Tree LinkBefore(ItemId item, ItemId? previous) =>
        this with { PreviousSibling = previous is { } previousId ? this.PreviousSibling.SetItem(item, previousId) : this.PreviousSibling.Remove(item) };

    private Tree WithFirst(ItemId? parent, ItemId? first) => parent switch
    {
        null => this with { FirstTopLevel = first },
        { } parentId => this with { FirstChild = first is { } firstId ? this.FirstChild.SetItem(parentId, firstId) : this.FirstChild.Remove(parentId) },
    };

    private List<ItemId> SiblingsFrom(ItemId? first)
    {
        var siblings = new List<ItemId>();
        for (var current = first; current is { } id; current = this.NextOf(id))
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
