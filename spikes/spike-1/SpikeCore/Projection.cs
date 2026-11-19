using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace SpikeCore;

public sealed class Item
{
    public string Id = "";
    public string? ParentId;          // null: top level
    public string Title = "";
    public string Note = "";
    public bool Deleted;
    public List<string>? Children;    // sibling order by reference (ADR-004); tombstones stay in the list
    public HashSet<string>? Tags;     // tag item ids (ADR-082)
    public bool? Symmetric;           // relation-type.set (ADR-084)
    public string? ReverseName;
}

public sealed class Relation
{
    public string Id = "";
    public string From = "";
    public string TypeId = "";
    public string To = "";
    public bool Removed;
    public string Note = "";
}

/// <summary>
/// Minimal replay with the data structures and complexity of the planned Domain (spike plan section 2):
/// dictionary of items, sibling order by reference, move with cycle rule, delete/restore as tombstones,
/// tags and relations with ids. Events are applied in the order given (the dataset is written in the agreed order).
/// </summary>
public sealed class Projection
{
    public readonly Dictionary<string, Item> Items = new(StringComparer.Ordinal);
    public readonly List<string> TopLevel = new();
    public readonly Dictionary<string, Relation> Relations = new(StringComparer.Ordinal);
    public string? InboxId, TagsBranchId, RelationTypesBranchId, DefaultRelationTypeId;
    public readonly Dictionary<string, string> Devices = new(StringComparer.Ordinal);
    public long EventsApplied;
    public long UnknownEvents;

    /// <summary>Optional observer (search index) told about title and note changes: (itemId, oldTitle, oldNote, newTitle, newNote).</summary>
    public Action<string, string, string, string, string>? TextChanged;

    public void Apply(EventRecord e)
    {
        EventsApplied++;
        var d = e.Data;
        switch (e.Type)
        {
            case "item.created":
            {
                var item = new Item { Id = d.ItemId!, ParentId = Empty(d.ParentId), Title = d.Title ?? "", Note = d.Note ?? "" };
                Items[item.Id] = item;
                Place(item, d.AfterItemId);
                TextChanged?.Invoke(item.Id, "", "", item.Title, item.Note);
                break;
            }
            case "item.title-changed":
                if (Items.TryGetValue(d.ItemId!, out var t)) { var old = t.Title; t.Title = d.Title ?? ""; TextChanged?.Invoke(t.Id, old, t.Note, t.Title, t.Note); }
                break;
            case "item.note-changed":
                if (Items.TryGetValue(d.ItemId!, out var n)) { var old = n.Note; n.Note = d.Note ?? ""; TextChanged?.Invoke(n.Id, n.Title, old, n.Title, n.Note); }
                break;
            case "item.moved":
                if (Items.TryGetValue(d.ItemId!, out var m))
                {
                    Siblings(m.ParentId).Remove(m.Id);
                    var newParent = Empty(d.ParentId);
                    m.ParentId = newParent != null && WouldCycle(m.Id, newParent) ? null : newParent;
                    Place(m, m.ParentId == newParent ? d.AfterItemId : null);
                }
                break;
            case "item.deleted":
                if (Items.TryGetValue(d.ItemId!, out var del)) del.Deleted = true;
                break;
            case "item.restored":
                if (Items.TryGetValue(d.ItemId!, out var res)) res.Deleted = false;
                break;
            case "item.tagged":
                if (Items.TryGetValue(d.ItemId!, out var tg)) (tg.Tags ??= new(StringComparer.Ordinal)).Add(d.TagId!);
                break;
            case "item.untagged":
                if (Items.TryGetValue(d.ItemId!, out var ut)) ut.Tags?.Remove(d.TagId!);
                break;
            case "relation.added":
                Relations[d.RelationId!] = new Relation { Id = d.RelationId!, From = d.FromItemId!, TypeId = d.TypeId!, To = d.ToItemId! };
                break;
            case "relation.removed":
                if (Relations.TryGetValue(d.RelationId!, out var rr)) rr.Removed = true;
                break;
            case "relation.restored":
                if (Relations.TryGetValue(d.RelationId!, out var rs)) rs.Removed = false;
                break;
            case "relation.type-changed":
                if (Relations.TryGetValue(d.RelationId!, out var rt)) rt.TypeId = d.TypeId!;
                break;
            case "relation.note-changed":
                if (Relations.TryGetValue(d.RelationId!, out var rn)) rn.Note = d.Note ?? "";
                break;
            case "relation-type.set":
                if (Items.TryGetValue(d.ItemId!, out var ty)) { ty.Symmetric = d.Symmetric; ty.ReverseName = d.ReverseName; }
                break;
            case "tree.inbox-set": InboxId = d.ItemId; break;
            case "tree.tags-set": TagsBranchId = d.ItemId; break;
            case "tree.relation-types-set": RelationTypesBranchId = d.ItemId; break;
            case "tree.default-relation-type-set": DefaultRelationTypeId = d.ItemId; break;
            case "device.joined": Devices[d.DeviceId!] = d.DeviceLabel ?? ""; break;
            default: UnknownEvents++; break;
        }
    }

    private static string? Empty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    private List<string> Siblings(string? parentId)
    {
        if (parentId == null) return TopLevel;
        var p = Items[parentId];
        return p.Children ??= new List<string>();
    }

    /// <summary>ADR-004: after the named sibling; first if empty or if the sibling is under another parent now.</summary>
    private void Place(Item item, string? afterItemId)
    {
        if (item.ParentId != null && !Items.ContainsKey(item.ParentId)) item.ParentId = null;
        var list = Siblings(item.ParentId);
        int at = 0;
        if (!string.IsNullOrEmpty(afterItemId) && Items.TryGetValue(afterItemId, out var after) && after.ParentId == item.ParentId)
        {
            int i = list.IndexOf(afterItemId);
            if (i >= 0) at = i + 1;
        }
        list.Insert(at, item.Id);
    }

    private bool WouldCycle(string itemId, string newParent)
    {
        string? p = newParent;
        while (p != null)
        {
            if (p == itemId) return true;
            p = Items.TryGetValue(p, out var it) ? it.ParentId : null;
        }
        return false;
    }

    /// <summary>The visible top-level titles (what the first render shows).</summary>
    public IEnumerable<Item> VisibleTopLevel() => TopLevel.Select(id => Items[id]).Where(i => !i.Deleted);

    public int VisibleCount()
    {
        int count = 0;
        var stack = new Stack<string>(TopLevel);
        while (stack.Count > 0)
        {
            var it = Items[stack.Pop()];
            if (it.Deleted) continue;
            count++;
            if (it.Children != null) foreach (var c in it.Children) stack.Push(c);
        }
        return count;
    }

    /// <summary>SHA-256 over the visible outline (depth, id, title, note, sorted tags) and the live relations; compares replays across runtimes.</summary>
    public string Checksum()
    {
        var sb = new StringBuilder();
        void Walk(List<string> ids, int depth)
        {
            foreach (var id in ids)
            {
                var it = Items[id];
                if (it.Deleted) continue;
                sb.Append(depth).Append('|').Append(id).Append('|').Append(it.Title).Append('|').Append(it.Note).Append('|');
                if (it.Tags != null) sb.AppendJoin(',', it.Tags.Order(StringComparer.Ordinal));
                sb.Append('\n');
                if (it.Children != null) Walk(it.Children, depth + 1);
            }
        }
        Walk(TopLevel, 0);
        foreach (var r in Relations.Values.Where(r => !r.Removed).OrderBy(r => r.Id, StringComparer.Ordinal))
            sb.Append("R|").Append(r.Id).Append('|').Append(r.From).Append('|').Append(r.TypeId).Append('|').Append(r.To).Append('|').Append(r.Note).Append('\n');
        sb.Append("M|").Append(InboxId).Append('|').Append(TagsBranchId).Append('|').Append(RelationTypesBranchId).Append('|').Append(DefaultRelationTypeId);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    // ---- snapshot ----

    public SnapshotDto ToSnapshot(long sequence) => new()
    {
        Sequence = sequence,
        EventsApplied = EventsApplied,
        TopLevel = TopLevel,
        InboxId = InboxId, TagsBranchId = TagsBranchId, RelationTypesBranchId = RelationTypesBranchId, DefaultRelationTypeId = DefaultRelationTypeId,
        Devices = Devices,
        Items = Items.Values.Select(i => new SnapshotItem
        {
            Id = i.Id, ParentId = i.ParentId, Title = i.Title, Note = i.Note.Length == 0 ? null : i.Note, Deleted = i.Deleted ? true : null,
            Children = i.Children, Tags = i.Tags?.ToList(), Symmetric = i.Symmetric, ReverseName = i.ReverseName,
        }).ToList(),
        Relations = Relations.Values.Select(r => new SnapshotRelation { Id = r.Id, From = r.From, TypeId = r.TypeId, To = r.To, Removed = r.Removed ? true : null, Note = r.Note.Length == 0 ? null : r.Note }).ToList(),
    };

    public static Projection FromSnapshot(SnapshotDto s)
    {
        var p = new Projection { InboxId = s.InboxId, TagsBranchId = s.TagsBranchId, RelationTypesBranchId = s.RelationTypesBranchId, DefaultRelationTypeId = s.DefaultRelationTypeId, EventsApplied = s.EventsApplied };
        p.TopLevel.AddRange(s.TopLevel);
        foreach (var kv in s.Devices) p.Devices[kv.Key] = kv.Value;
        p.Items.EnsureCapacity(s.Items.Count);
        foreach (var i in s.Items)
            p.Items[i.Id] = new Item
            {
                Id = i.Id, ParentId = i.ParentId, Title = i.Title, Note = i.Note ?? "", Deleted = i.Deleted == true,
                Children = i.Children, Tags = i.Tags == null ? null : new HashSet<string>(i.Tags, StringComparer.Ordinal), Symmetric = i.Symmetric, ReverseName = i.ReverseName,
            };
        foreach (var r in s.Relations)
            p.Relations[r.Id] = new Relation { Id = r.Id, From = r.From, TypeId = r.TypeId, To = r.To, Removed = r.Removed == true, Note = r.Note ?? "" };
        return p;
    }
}

public sealed class SnapshotDto
{
    [JsonPropertyName("seq")] public long Sequence { get; set; }
    [JsonPropertyName("applied")] public long EventsApplied { get; set; }
    [JsonPropertyName("top")] public List<string> TopLevel { get; set; } = new();
    [JsonPropertyName("inbox")] public string? InboxId { get; set; }
    [JsonPropertyName("tags")] public string? TagsBranchId { get; set; }
    [JsonPropertyName("relTypes")] public string? RelationTypesBranchId { get; set; }
    [JsonPropertyName("defaultType")] public string? DefaultRelationTypeId { get; set; }
    [JsonPropertyName("devices")] public Dictionary<string, string> Devices { get; set; } = new();
    [JsonPropertyName("items")] public List<SnapshotItem> Items { get; set; } = new();
    [JsonPropertyName("relations")] public List<SnapshotRelation> Relations { get; set; } = new();
}

public sealed class SnapshotItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("p")] public string? ParentId { get; set; }
    [JsonPropertyName("t")] public string Title { get; set; } = "";
    [JsonPropertyName("n")] public string? Note { get; set; }
    [JsonPropertyName("d")] public bool? Deleted { get; set; }
    [JsonPropertyName("c")] public List<string>? Children { get; set; }
    [JsonPropertyName("g")] public List<string>? Tags { get; set; }
    [JsonPropertyName("s")] public bool? Symmetric { get; set; }
    [JsonPropertyName("r")] public string? ReverseName { get; set; }
}

public sealed class SnapshotRelation
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("f")] public string From { get; set; } = "";
    [JsonPropertyName("y")] public string TypeId { get; set; } = "";
    [JsonPropertyName("t")] public string To { get; set; } = "";
    [JsonPropertyName("x")] public bool? Removed { get; set; }
    [JsonPropertyName("n")] public string? Note { get; set; }
}
