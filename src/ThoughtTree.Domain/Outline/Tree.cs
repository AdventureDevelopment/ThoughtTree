using System.Collections;
using System.Collections.Immutable;

namespace ThoughtTree.Domain.Outline;

/// <summary>The tree as a projection of the event log, immutable (ADR-002); <see cref="TreeBuilder"/> makes the changes.</summary>
public sealed record Tree
{
    private Tree()
    {
    }

    internal Tree(
        ImmutableDictionary<ItemId, Item> items,
        ItemId? firstTopLevel,
        ImmutableDictionary<ItemId, ItemId> firstChild,
        ImmutableDictionary<ItemId, ItemId> nextSibling,
        ImmutableDictionary<ItemId, ItemId> previousSibling,
        ImmutableList<Anomaly> reports)
    {
        this.Items = items;
        this.FirstTopLevel = firstTopLevel;
        this.FirstChild = firstChild;
        this.NextSibling = nextSibling;
        this.PreviousSibling = previousSibling;
        this.Reports = reports;
    }

    public static Tree Empty { get; } = new();

    internal ImmutableDictionary<ItemId, Item> Items { get; private init; } = ImmutableDictionary<ItemId, Item>.Empty;

    internal ItemId? FirstTopLevel { get; private init; }

    internal ImmutableDictionary<ItemId, ItemId> FirstChild { get; private init; } = ImmutableDictionary<ItemId, ItemId>.Empty;

    internal ImmutableDictionary<ItemId, ItemId> NextSibling { get; private init; } = ImmutableDictionary<ItemId, ItemId>.Empty;

    internal ImmutableDictionary<ItemId, ItemId> PreviousSibling { get; private init; } = ImmutableDictionary<ItemId, ItemId>.Empty;

    /// <summary>Grouped by item id, those without an item last; agreed order inside a group (model rules 18, 19).</summary>
    public IReadOnlyList<Anomaly> Anomalies => this.Reports;

    internal ImmutableList<Anomaly> Reports { get; private init; } = [];

    /// <summary>Deleted items included (INV-5).</summary>
    public IReadOnlyCollection<Item> AllItems => new ItemCollection(this.Items);

    /// <summary>Deleted items included.</summary>
    public IReadOnlyList<ItemId> TopLevelOrder => this.SiblingsFrom(this.FirstOf(null));

    /// <summary>Deleted items included.</summary>
    public IReadOnlyList<ItemId> ChildrenOf(ItemId id) => this.SiblingsFrom(this.FirstOf(id));

    public Item? Find(ItemId id) => this.Items.GetValueOrDefault(id);

    /// <summary>Items not deleted and without a deleted ancestor, in outline order.</summary>
    public IReadOnlyList<(Depth Depth, Item Item)> VisibleOutline()
    {
        var result = new List<(Depth, Item)>();
        Walk(this.TopLevelOrder, Depth.TopLevel);
        return result;

        void Walk(IReadOnlyList<ItemId> ids, Depth depth)
        {
            foreach (var id in ids)
            {
                var item = this.Items[id];
                if (item.Deleted)
                {
                    continue;
                }

                result.Add((depth, item));
                Walk(this.ChildrenOf(id), depth.Below());
            }
        }
    }

    /// <summary>The visible titles as an indented outline.</summary>
    public string ToOutline() =>
        string.Join('\n', this.VisibleOutline().Select(entry => new string(' ', entry.Depth.Levels * 2) + "- " + entry.Item.Title));

    public override string ToString() => $"Tree with {this.Items.Count} items";

    internal TreeBuilder ToBuilder() => new(this);

    private ItemId? NextOf(ItemId id) => this.NextSibling.TryGetValue(id, out var next) ? next : null;

    private ItemId? FirstOf(ItemId? parent) =>
        parent is { } parentId ? (this.FirstChild.TryGetValue(parentId, out var first) ? first : null) : this.FirstTopLevel;

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
