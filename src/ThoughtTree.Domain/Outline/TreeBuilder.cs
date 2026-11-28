using System.Collections.Immutable;

namespace ThoughtTree.Domain.Outline;

/// <summary>
/// Changes a tree in place for one run of the projection and hands out immutable trees on request; nodes are copied once
/// per run instead of once per event (PF6). Never shared: the projection creates one per run and keeps only its trees.
/// </summary>
internal sealed class TreeBuilder
{
    private readonly ImmutableDictionary<ItemId, Item>.Builder items;

    private readonly ImmutableDictionary<ItemId, ItemId>.Builder firstChild;

    private readonly ImmutableDictionary<ItemId, ItemId>.Builder nextSibling;

    private readonly ImmutableDictionary<ItemId, ItemId>.Builder previousSibling;

    private readonly ImmutableList<Anomaly>.Builder reports;

    private ItemId? firstTopLevel;

    internal TreeBuilder(Tree tree)
    {
        this.items = tree.Items.ToBuilder();
        this.firstTopLevel = tree.FirstTopLevel;
        this.firstChild = tree.FirstChild.ToBuilder();
        this.nextSibling = tree.NextSibling.ToBuilder();
        this.previousSibling = tree.PreviousSibling.ToBuilder();
        this.reports = tree.Reports.ToBuilder();
    }

    internal Item this[ItemId id] => this.items[id];

    internal bool Contains(ItemId id) => this.items.ContainsKey(id);

    internal Tree ToImmutable() => new(
        this.items.ToImmutable(),
        this.firstTopLevel,
        this.firstChild.ToImmutable(),
        this.nextSibling.ToImmutable(),
        this.previousSibling.ToImmutable(),
        this.reports.ToImmutable());

    internal void SetItem(Item item) => this.items[item.Id] = item;

    internal void AddAnomaly(AnomalyKind kind, TreeEvent treeEvent, ItemId? item) =>
        this.reports.Insert(this.EndOfReportGroup(item), new Anomaly(kind, treeEvent.EventId, item));

    internal bool IsSameOrDescendant(ItemId candidate, ItemId ancestor)
    {
        for (ItemId? current = candidate; current is { } id; current = this.items[id].Parent)
        {
            if (id == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    internal bool IsUnknownParent(ItemId? parent) => parent is { } parentId && !this.items.ContainsKey(parentId);

    internal bool IsUnusableAfter(ItemId? after, ItemId item) => after is { } afterId && (afterId == item || !this.items.ContainsKey(afterId));

    internal void RemoveChild(ItemId id)
    {
        var previous = this.PreviousOf(id);
        var next = this.NextOf(id);
        this.nextSibling.Remove(id);
        this.previousSibling.Remove(id);

        if (previous is { } before)
        {
            this.LinkAfter(before, next);
        }
        else
        {
            this.SetFirst(this.items[id].Parent, next);
        }

        if (next is { } following)
        {
            this.LinkBefore(following, previous);
        }
    }

    internal void InsertChild(ItemId? parent, ItemId id, ItemId? after)
    {
        if (after is { } before && this.items[before].Parent == parent)
        {
            var next = this.NextOf(before);
            this.LinkAfter(before, id);
            this.LinkBefore(id, before);
            this.LinkAfter(id, next);
            if (next is { } following)
            {
                this.LinkBefore(following, id);
            }

            return;
        }

        var first = this.FirstOf(parent);
        this.SetFirst(parent, id);
        this.LinkAfter(id, first);
        if (first is { } oldFirst)
        {
            this.LinkBefore(oldFirst, id);
        }
    }

    private int EndOfReportGroup(ItemId? item)
    {
        var (low, high) = (0, this.reports.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (CompareReportGroups(this.reports[middle].ItemId, item) <= 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static int CompareReportGroups(ItemId? first, ItemId? second) => (first, second) switch
    {
        (null, null) => 0,
        (null, { }) => 1,
        ({ }, null) => -1,
        ({ } firstId, { } secondId) => string.CompareOrdinal(firstId.Value, secondId.Value),
    };

    private ItemId? NextOf(ItemId id) => this.nextSibling.TryGetValue(id, out var next) ? next : null;

    private ItemId? PreviousOf(ItemId id) => this.previousSibling.TryGetValue(id, out var previous) ? previous : null;

    private ItemId? FirstOf(ItemId? parent) =>
        parent is { } parentId ? (this.firstChild.TryGetValue(parentId, out var first) ? first : null) : this.firstTopLevel;

    private void LinkAfter(ItemId item, ItemId? next)
    {
        if (next is { } nextId)
        {
            this.nextSibling[item] = nextId;
        }
        else
        {
            this.nextSibling.Remove(item);
        }
    }

    private void LinkBefore(ItemId item, ItemId? previous)
    {
        if (previous is { } previousId)
        {
            this.previousSibling[item] = previousId;
        }
        else
        {
            this.previousSibling.Remove(item);
        }
    }

    private void SetFirst(ItemId? parent, ItemId? first)
    {
        if (parent is not { } parentId)
        {
            this.firstTopLevel = first;
        }
        else if (first is { } firstId)
        {
            this.firstChild[parentId] = firstId;
        }
        else
        {
            this.firstChild.Remove(parentId);
        }
    }
}
