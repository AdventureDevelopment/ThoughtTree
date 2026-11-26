using System.Globalization;
using System.Text;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Properties.Outline;

/// <summary>Renders a <see cref="Tree"/> in the reference model's canonical text, so the two compare as strings.</summary>
public static class CanonicalTree
{
    public static string Render(Tree tree)
    {
        var lines = new List<string> { $"items {tree.AllItems.Count}" };
        foreach (var item in tree.AllItems.OrderBy(i => i.Id.Value, StringComparer.Ordinal))
        {
            var siblings = item.Parent is { } p ? tree.ChildrenOf(p) : tree.TopLevelOrder;
            lines.Add($"item {Quote(item.Id.Value)} parent {(item.Parent is { } q ? Quote(q.Value) : "none")} " +
                $"position {IndexOf(siblings, item.Id)} deleted {Bool(item.Deleted)} visible {Bool(IsVisible(tree, item))} " +
                $"title {Quote(item.Title)} note {Quote(item.Note)}");
        }

        lines.Add(string.Concat(tree.TopLevelOrder.Select(id => " " + Quote(id.Value)).Prepend("top")));
        lines.Add($"reports {tree.Anomalies.Count}");
        lines.AddRange(tree.Anomalies.Select(a =>
            $"report {KindName(a.Kind)} event {Quote(a.EventId.Value)} item {(a.Item is { } i ? Quote(i.Value) : "none")}"));
        return string.Join('\n', lines);
    }

    private static string KindName(AnomalyKind kind) => kind switch
    {
        AnomalyKind.DuplicateCreate => "duplicate-create",
        AnomalyKind.UnknownItem => "unknown-item",
        AnomalyKind.UnknownParent => "unknown-parent",
        AnomalyKind.UnknownAfter => "unknown-after",
        AnomalyKind.Cycle => "cycle",
        AnomalyKind.UnknownEventType => "unknown-event-type",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static int IndexOf(IReadOnlyList<ItemId> ids, ItemId id)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsVisible(Tree tree, Item item)
    {
        for (Item? current = item; current is not null; current = current.Parent is { } p ? tree.Find(p) : null)
        {
            if (current.Deleted)
            {
                return false;
            }
        }

        return true;
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Quote(string? s)
    {
        if (s is null)
        {
            return "null";
        }

        var sb = new StringBuilder("\"");
        foreach (var ch in s)
        {
            _ = ch switch
            {
                '"' => sb.Append("\\\""),
                '\\' => sb.Append("\\\\"),
                >= ' ' and <= '~' => sb.Append(ch),
                _ => sb.Append("\\u").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture)),
            };
        }

        return sb.Append('"').ToString();
    }
}
