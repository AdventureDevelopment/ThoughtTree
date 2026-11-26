using System.Globalization;
using System.Text;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Properties.Outline;

/// <summary>Renders a <see cref="Tree"/> in the reference model's canonical text, so the two compare as strings.</summary>
public static class CanonicalTree
{
    public static string OfReplay(IEnumerable<TreeEvent> events) => Render(Projection.Replay(events));

    public static string Render(Tree tree)
    {
        var lines = new List<string> { $"items {tree.AllItems.Count}" };
        foreach (var item in tree.AllItems.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            var siblings = item.Parent is { } parent ? tree.ChildrenOf(parent) : tree.TopLevelOrder;
            lines.Add($"item {Quote(item.Id.Value)} parent {(item.Parent is { } parentId ? Quote(parentId.Value) : "none")} " +
                $"position {IndexOf(siblings, item.Id)} deleted {Bool(item.Deleted)} visible {Bool(IsVisible(tree, item))} " +
                $"title {Quote(item.Title.Value)} note {Quote(item.Note.Value)}");
        }

        lines.Add(string.Concat(tree.TopLevelOrder.Select(id => " " + Quote(id.Value)).Prepend("top")));
        lines.Add($"reports {tree.Anomalies.Count}");
        lines.AddRange(tree.Anomalies.Select(report =>
            $"report {KindName(report.Kind)} event {Quote(report.EventId.Value)} item {(report.ItemId is { } reportedItem ? Quote(reportedItem.Value) : "none")}"));
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
        for (var index = 0; index < ids.Count; index++)
        {
            if (ids[index] == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsVisible(Tree tree, Item item)
    {
        for (Item? current = item; current is not null; current = current.Parent is { } parent ? tree.Find(parent) : null)
        {
            if (current.Deleted)
            {
                return false;
            }
        }

        return true;
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Quote(string text)
    {
        var builder = new StringBuilder("\"");
        foreach (var character in text)
        {
            _ = character switch
            {
                '"' => builder.Append("\\\""),
                '\\' => builder.Append("\\\\"),
                >= ' ' and <= '~' => builder.Append(character),
                _ => builder.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture)),
            };
        }

        return builder.Append('"').ToString();
    }
}
