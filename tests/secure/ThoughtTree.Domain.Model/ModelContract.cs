using System.Text;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Model;

/// <summary>
/// The contract between the reference model and the property tests. The model returns the whole tree as
/// canonical text so two trees are equal exactly when the texts are equal.
/// Deliberately naive: sort everything, replay one by one over plain lists and dictionaries.
/// </summary>
public static class ReferenceModel
{
    private sealed class Item
    {
        public string Id = "";
        public string? Parent;          // null = top level
        public bool Deleted;
        public string Title = "";
        public string Note = "";
    }

    /// <summary>Sorts all events by the agreed order, drops duplicate event ids, replays one by one.</summary>
    public static string ReplayToCanonicalText(IEnumerable<TreeEvent> events)
    {
        // Duplicate EventIds: the first one seen is kept (INV-13 says duplicates are identical; if not, the first wins).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var all = new List<TreeEvent>();
        foreach (var e in events)
        {
            if (seen.Add(e.EventId))
            {
                all.Add(e);
            }
        }

        // Agreed order: clock time, counter, deviceId lowercase ordinal; ties broken by EventId ordinal. Of events
        // sharing all three, all are applied in that order; nothing is reported.
        all.Sort((a, b) =>
        {
            int c = a.Clock.Time.CompareTo(b.Clock.Time);
            if (c != 0)
            {
                return c;
            }

            c = a.Clock.Counter.CompareTo(b.Clock.Counter);
            if (c != 0)
            {
                return c;
            }

            c = string.CompareOrdinal(Dev(a), Dev(b));
            if (c != 0)
            {
                return c;
            }

            return string.CompareOrdinal(a.EventId, b.EventId);
        });

        var items = new Dictionary<string, Item>(StringComparer.Ordinal);
        var top = new List<string>();                                    // ids of top-level siblings in order
        var kids = new Dictionary<string, List<string>>(StringComparer.Ordinal); // parent id -> ordered child ids

        List<string> SiblingsOf(string? parent)
        {
            if (parent == null)
            {
                return top;
            }

            if (!kids.TryGetValue(parent, out var l))
            {
                kids[parent] = l = new List<string>();
            }

            return l;
        }

        // Normalise a requested parent: empty/null = top level; unknown = top level (no policy in the rules for
        // ItemCreated with an unknown parent; ADR-023 / the task say moves go to top level, same reading used here).
        string? ResolveParent(ItemId? p) =>
            p is { } v && !string.IsNullOrEmpty(v.Value) && items.ContainsKey(v.Value) ? v.Value : null;

        void Place(Item item, string? parent, ItemId? after)
        {
            var list = SiblingsOf(parent);
            int index = 0; // first, unless 'after' is a sibling here right now (tombstones count)
            if (after is { } a && !string.IsNullOrEmpty(a.Value) && a.Value != item.Id)
            {
                int at = list.IndexOf(a.Value);
                if (at >= 0)
                {
                    index = at + 1;
                }
            }
            list.Insert(index, item.Id);
            item.Parent = parent;
        }

        var anomalies = new List<(string Kind, string EventId)>();
        foreach (var e in all)
        {
            switch (e)
            {
                case ItemCreated c:
                {
                    if (string.IsNullOrEmpty(c.Item.Value))
                        {
                            break; // empty id: silently ignored (no rule)
                        }

                        if (items.ContainsKey(c.Item.Value)) { anomalies.Add(("duplicate-create", e.EventId)); break; }
                    var item = new Item { Id = c.Item.Value, Title = c.Title ?? "", Note = c.Note ?? "" };
                    // Parent resolved before the item is registered, so an item cannot be its own parent.
                    var parent = ResolveParent(c.Parent);
                    items[item.Id] = item;
                    Place(item, parent, c.After);
                    break;
                }
                case ItemTitleChanged t:
                    if (items.TryGetValue(t.Item.Value ?? "", out var ti))
                    {
                        ti.Title = t.Title ?? "";
                    }
                    else
                    {
                        anomalies.Add(("unknown-item", e.EventId));
                    }

                    break;
                case ItemNoteChanged n:
                    if (items.TryGetValue(n.Item.Value ?? "", out var ni))
                    {
                        ni.Note = n.Note ?? "";
                    }
                    else
                    {
                        anomalies.Add(("unknown-item", e.EventId));
                    }

                    break;
                case ItemDeleted d:
                    if (items.TryGetValue(d.Item.Value ?? "", out var di))
                    {
                        di.Deleted = true;
                    }
                    else
                    {
                        anomalies.Add(("unknown-item", e.EventId)); // idempotent; children untouched
                    }

                    break;
                case ItemRestored r:
                    if (items.TryGetValue(r.Item.Value ?? "", out var ri))
                    {
                        ri.Deleted = false;
                    }
                    else
                    {
                        anomalies.Add(("unknown-item", e.EventId)); // only this item, not its subtree
                    }

                    break;
                case ItemMoved m:
                {
                    if (!items.TryGetValue(m.Item.Value ?? "", out var mi)) { anomalies.Add(("unknown-item", e.EventId)); break; }
                    var parent = ResolveParent(m.Parent);
                    // Cycle: walk up from the requested parent; reaching the moved item means top level instead.
                    for (var p = parent; p != null; p = items[p].Parent)
                        {
                            if (p == mi.Id) { parent = null; break; }
                        }

                        SiblingsOf(mi.Parent).Remove(mi.Id);
                    // 'after' naming the item itself is treated as "not a sibling": first under the parent.
                    Place(mi, parent, m.After);
                    break;
                }
            }
        }

        // Canonical text: depth-first over the whole tree including tombstones.
        var sb = new StringBuilder();
        var stack = new Stack<(string Id, int Depth)>();
        for (int i = top.Count - 1; i >= 0; i--)
        {
            stack.Push((top[i], 0));
        }

        while (stack.Count > 0)
        {
            var (id, depth) = stack.Pop();
            var it = items[id];
            sb.Append(depth).Append(' ').Append(Quote(id)).Append(' ')
              .Append(it.Parent == null ? "-" : Quote(it.Parent)).Append(' ')
              .Append(it.Deleted ? "deleted" : "live").Append(' ')
              .Append(Quote(it.Title)).Append(' ').Append(Quote(it.Note)).Append('\n');
            if (kids.TryGetValue(id, out var ch))
            {
                for (int i = ch.Count - 1; i >= 0; i--)
                {
                    stack.Push((ch[i], depth + 1));
                }
            }
        }
        anomalies.Sort((a, b) => { int c = string.CompareOrdinal(a.Kind, b.Kind); return c != 0 ? c : string.CompareOrdinal(a.EventId, b.EventId); });
        foreach (var (kind, id) in anomalies)
        {
            sb.Append("anomaly ").Append(kind).Append(' ').Append(Quote(id)).Append('\n');
        }

        return sb.ToString();
    }

    private static string Dev(TreeEvent e) => (e.DeviceId ?? "").ToLowerInvariant();

    // JSON-style quoting; everything outside printable ASCII becomes \uXXXX per UTF-16 unit, so any string is unambiguous.
    private static string Quote(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char ch in s)
        {
            if (ch == '"')
            {
                sb.Append("\\\"");
            }
            else if (ch == '\\')
            {
                sb.Append("\\\\");
            }
            else if (ch >= ' ' && ch <= '~')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append("\\u").Append(((int)ch).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        return sb.Append('"').ToString();
    }
}
