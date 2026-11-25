using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

internal static class TreeText
{
    /// <summary>The whole tree, deleted items included: depth, id, parent, state, title, note, then anomalies.</summary>
    public static string Full(Tree tree)
    {
        var lines = new List<string>();
        Walk(tree.TopLevelOrder, 0);
        lines.AddRange(tree.Anomalies.Select(a => $"anomaly {a.Kind} {a.EventId}"));
        return string.Join('\n', lines);

        void Walk(IReadOnlyList<ItemId> ids, int depth)
        {
            foreach (ItemId id in ids)
            {
                Item item = tree.Find(id)!;
                lines.Add($"{depth} {id} parent={item.Parent?.ToString() ?? "-"} {(item.Deleted ? "deleted" : "live")} '{item.Title}' '{item.Note}'");
                Walk(tree.ChildrenOf(id), depth + 1);
            }
        }
    }
}
