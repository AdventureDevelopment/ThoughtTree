using System.Globalization;
using System.Text;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Model;

/// <summary>Naive reference replay of an event log into canonical text, by the rules in RULES.md (R-n), the referee for the real projection (ADR-035).</summary>
/// <remarks>
/// Canonical text: lines joined by "\n", no trailing newline.
/// <code>
/// items N
/// item S parent S|none position K deleted B visible B title S note S   (one per created item, sorted by id, ordinal)
/// top S S ...                                                         (top-level ids in order; "top" alone when empty)
/// reports M
/// report KIND event S item S|none                                     (in the order of R18 and R19)
/// </code>
/// S is a quoted string: '"', then each UTF-16 unit as itself if in 0x20..0x7E other than '"' and '\',
/// else '\"', '\\' or '\uXXXX' (4 uppercase hex digits), then '"'. A null string renders as the bare token null.
/// K is the zero-based index among all children of the same parent, deleted ones included; B is true or false.
/// KIND is one of duplicate-create, unknown-item, unknown-parent, unknown-after, cycle, unknown-event-type.
/// </remarks>
public static class ReferenceModel
{

    public static string ReplayToCanonicalText(IEnumerable<TreeEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var state = new State();
        foreach (var e in Order(events))
        {
            state.Apply(e);
        }

        return state.Render();
    }

    private static List<TreeEvent> Order(IEnumerable<TreeEvent> events)
    {
        // R1
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<TreeEvent>();
        foreach (var e in events)
        {
            if (seen.Add(e.EventId.Value))
            {
                kept.Add(e);
            }
        }

        // R2, R3
        kept.Sort((a, b) =>
        {
            var byClock = a.Clock.Value.CompareTo(b.Clock.Value);
            return byClock != 0 ? byClock : string.CompareOrdinal(a.EventId.Value, b.EventId.Value);
        });
        return kept;
    }

    private static string? IdOrNone(ItemId? id) =>
        id is null || string.IsNullOrEmpty(id.Value.Value) ? null : id.Value.Value;

    private static string Quote(string? s)
    {
        if (s is null)
        {
            return "null";
        }

        var sb = new StringBuilder();
        sb.Append('"');
        foreach (var c in s)
        {
            if (c == '"')
            {
                sb.Append("\\\"");
            }
            else if (c == '\\')
            {
                sb.Append("\\\\");
            }
            else if (c >= 0x20 && c <= 0x7E)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    private sealed class Node
    {
        public required string Id { get; init; }

        public string? Parent { get; set; }

        public bool Deleted { get; set; }

        public string Title { get; set; } = "";

        public string Note { get; set; } = "";
    }

    private sealed record Report(string Kind, string EventId, string? Item);

    private sealed class State
    {
        private readonly Dictionary<string, Node> items = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> children = new(StringComparer.Ordinal);
        private readonly List<string> topLevel = [];
        private readonly List<Report> reports = [];

        public void Apply(TreeEvent e)
        {
            switch (e)
            {
                case ItemCreated c:
                    this.Create(c);
                    break;
                case ItemTitleChanged t:
                    if (this.Existing(t, t.Item) is { } titled && t.Title is not null)
                    {
                        titled.Title = t.Title; // R7, R9
                    }

                    break;
                case ItemNoteChanged n:
                    if (this.Existing(n, n.Item) is { } noted && n.Note is not null)
                    {
                        noted.Note = n.Note; // R7, R9
                    }

                    break;
                case ItemMoved m:
                    this.Move(m);
                    break;
                case ItemDeleted d:
                    if (this.Existing(d, d.Item) is { } deleted)
                    {
                        deleted.Deleted = true; // R8
                    }

                    break;
                case ItemRestored r:
                    if (this.Existing(r, r.Item) is { } restored)
                    {
                        restored.Deleted = false; // R8
                    }

                    break;
                default:
                    this.reports.Add(new Report("unknown-event-type", e.EventId.Value, null)); // R16
                    break;
            }
        }

        public string Render()
        {
            var lines = new List<string>();
            var ids = this.items.Keys.ToList();
            ids.Sort(string.CompareOrdinal);
            lines.Add("items " + ids.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var id in ids)
            {
                var node = this.items[id];
                var position = this.SiblingsOf(node.Parent).IndexOf(id);
                lines.Add(
                    "item " + Quote(id)
                    + " parent " + (node.Parent is null ? "none" : Quote(node.Parent))
                    + " position " + position.ToString(CultureInfo.InvariantCulture)
                    + " deleted " + (node.Deleted ? "true" : "false")
                    + " visible " + (this.IsVisible(node) ? "true" : "false")
                    + " title " + Quote(node.Title)
                    + " note " + Quote(node.Note));
            }

            var top = new StringBuilder("top");
            foreach (var id in this.topLevel)
            {
                top.Append(' ').Append(Quote(id));
            }

            lines.Add(top.ToString());

            // R18, R19: the sort is stable, so replay order stays inside a group.
            var ordered = this.reports
                .OrderBy(r => r.Item is null ? 1 : 0)
                .ThenBy(r => r.Item ?? "", StringComparer.Ordinal)
                .ToList();
            lines.Add("reports " + ordered.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var r in ordered)
            {
                // R17
                lines.Add("report " + r.Kind + " event " + Quote(r.EventId) + " item " + (r.Item is null ? "none" : Quote(r.Item)));
            }

            return string.Join("\n", lines);
        }

        private void Create(ItemCreated c)
        {
            var id = c.Item.Value;
            if (this.items.ContainsKey(id))
            {
                this.reports.Add(new Report("duplicate-create", c.EventId.Value, id)); // R5
                return;
            }

            var parent = IdOrNone(c.Parent);
            if (parent is not null && !this.items.ContainsKey(parent))
            {
                this.reports.Add(new Report("unknown-parent", c.EventId.Value, id)); // R12
                parent = null;
            }

            var after = IdOrNone(c.After);
            if (after is not null && (after == id || !this.items.ContainsKey(after)))
            {
                this.reports.Add(new Report("unknown-after", c.EventId.Value, id)); // R13
                after = null;
            }

            // R4
            this.items[id] = new Node { Id = id, Parent = parent, Title = c.Title ?? "", Note = c.Note ?? "" };
            this.Place(id, parent, after);
        }

        private void Move(ItemMoved m)
        {
            var node = this.Existing(m, m.Item);
            if (node is null)
            {
                return;
            }

            var id = node.Id;
            var parent = IdOrNone(m.Parent);
            var after = IdOrNone(m.After);
            if (parent is not null && !this.items.ContainsKey(parent))
            {
                this.reports.Add(new Report("unknown-parent", m.EventId.Value, id)); // R14
                return;
            }

            if (after is not null && (after == id || !this.items.ContainsKey(after)))
            {
                this.reports.Add(new Report("unknown-after", m.EventId.Value, id)); // R14
                return;
            }

            if (parent is not null && this.IsSelfOrDescendant(parent, id))
            {
                this.reports.Add(new Report("cycle", m.EventId.Value, id)); // R15
                return;
            }

            this.SiblingsOf(node.Parent).Remove(id);
            node.Parent = parent;
            this.Place(id, parent, after);
        }

        private Node? Existing(TreeEvent e, ItemId item)
        {
            if (this.items.TryGetValue(item.Value, out var node))
            {
                return node;
            }

            this.reports.Add(new Report("unknown-item", e.EventId.Value, item.Value)); // R6
            return null;
        }

        private void Place(string id, string? parent, string? after)
        {
            var siblings = this.SiblingsOf(parent);

            // R10, R11
            var index = after is null ? -1 : siblings.IndexOf(after);
            siblings.Insert(index + 1, id);
        }

        private List<string> SiblingsOf(string? parent)
        {
            if (parent is null)
            {
                return this.topLevel;
            }

            if (!this.children.TryGetValue(parent, out var list))
            {
                list = [];
                this.children[parent] = list;
            }

            return list;
        }

        private bool IsSelfOrDescendant(string candidate, string ancestor)
        {
            for (var current = candidate; current is not null; current = this.items[current].Parent)
            {
                if (current == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsVisible(Node node)
        {
            // R9
            for (var current = node; ; current = this.items[current.Parent])
            {
                if (current.Deleted)
                {
                    return false;
                }

                if (current.Parent is null)
                {
                    return true;
                }
            }
        }
    }
}
