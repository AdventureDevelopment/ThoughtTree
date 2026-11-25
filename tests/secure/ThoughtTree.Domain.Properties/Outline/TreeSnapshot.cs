using System.Text;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Properties.Outline;

/// <summary>
/// Renders the whole state of a <see cref="Tree"/> as text: every item (deleted too) with parent, title, note,
/// deleted flag, in sibling order including deleted siblings. The format equals the reference model's canonical text (aligned with ModelContract.cs).
/// </summary>
public static class TreeSnapshot
{
    public static string Render(Tree tree)
    {
        var sb = new StringBuilder();
        var seen = new HashSet<ItemId>();
        Walk(tree.TopLevelOrder, 0);
        foreach (Item orphan in tree.AllItems.Where(i => !seen.Contains(i.Id)).OrderBy(i => i.Id.Value, StringComparer.Ordinal))
        {
            sb.Append("unreachable ").AppendLine(Line(orphan));
        }

        foreach (Anomaly a in tree.Anomalies)
        {
            sb.Append("anomaly ").Append(KindName(a.Kind)).Append(' ').Append(Quote(a.EventId)).Append('\n');
        }

        return sb.ToString();

        void Walk(IReadOnlyList<ItemId> ids, int depth)
        {
            foreach (ItemId id in ids)
            {
                Item item = tree.Find(id)!;
                if (!seen.Add(id))
                {
                    sb.Append("DUPLICATE ").AppendLine(id.Value);
                    continue;
                }

                sb.Append(depth).Append(' ').AppendLine(Line(item));
                Walk(tree.ChildrenOf(id), depth + 1);
            }
        }
    }

    public static string KindName(AnomalyKind kind) => kind switch
    {
        AnomalyKind.DuplicateCreate => "duplicate-create",
        AnomalyKind.UnknownItem => "unknown-item",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // Same as the reference model's canonical text: `depth "id" "parent"|- live|deleted "title" "note"`.
    private static string Line(Item i) =>
        $"{Quote(i.Id.Value)} {(i.Parent is { } p ? Quote(p.Value) : "-")} {(i.Deleted ? "deleted" : "live")} {Quote(i.Title)} {Quote(i.Note)}";

    private static string Quote(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char ch in s)
        {
            if (ch == '"') sb.Append("\\\"");
            else if (ch == '\\') sb.Append("\\\\");
            else if (ch >= ' ' && ch <= '~') sb.Append(ch);
            else sb.Append("\\u").Append(((int)ch).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
        }

        return sb.Append('"').ToString();
    }
}
