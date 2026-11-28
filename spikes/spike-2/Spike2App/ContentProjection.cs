using System.Security.Cryptography;
using System.Text;
using SpikeCore;

namespace Spike2App;

public sealed class CItem
{
    public string Id = "";
    public string? ParentId;
    public string? Title;            // null: text not loaded yet, or erased
    public string? Note;
    public bool Deleted;
    public List<string>? Children;
    public HashSet<string>? Tags;
    public bool? Symmetric;
    public string? ReverseName;
    public string? TitleBlob, NoteBlob, ReverseNameBlob;
    public List<(string BlobId, int Seq)>? History;   // every text event about the item (erase, case 5)
}

public sealed class CRelation
{
    public string Id = "";
    public string From = "", TypeId = "", To = "";
    public bool Removed;
    public string? Note;
    public string? NoteBlob;
    public List<(string BlobId, int Seq)>? History;
}

/// <summary>
/// Spike 1's throwaway replay (spikes/spike-1/SpikeCore/Projection.cs), extended for content blobs: text may arrive
/// with the event (layout 1), joined from its blob (layouts 2 and 3), or later (case 4), so every text field
/// remembers the blob it comes from. Same tree rules, same checksum as spike 1 (null text counts as empty).
/// </summary>
public sealed class ContentProjection
{
    public readonly Dictionary<string, CItem> Items = new(StringComparer.Ordinal);
    public readonly List<string> TopLevel = new();
    public readonly Dictionary<string, CRelation> Relations = new(StringComparer.Ordinal);
    public string? InboxId, TagsBranchId, RelationTypesBranchId, DefaultRelationTypeId;
    public readonly Dictionary<string, string?> Devices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _deviceLabelBlob = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _blobOwner = new(StringComparer.Ordinal);
    public long EventsApplied;
    public bool TrackHistory;

    /// <summary>Applies one event; <paramref name="content"/> is its joined blob (null: text inline, missing or not loaded).</summary>
    public void Apply(string eventId, int seq, EventRecord e, Content? content)
    {
        EventsApplied++;
        if (content != null) ContentFormat.Merge(e, content);
        var d = e.Data;
        switch (e.Type)
        {
            case "item.created":
            {
                var blob = ContentFormat.BlobIdOf(eventId);
                var item = new CItem { Id = d.ItemId!, ParentId = Empty(d.ParentId), Title = d.Title, Note = d.Title != null ? d.Note ?? "" : d.Note, TitleBlob = blob, NoteBlob = blob };
                Items[item.Id] = item;
                Place(item, d.AfterItemId);
                Own(blob, item, seq, ref item.History);
                break;
            }
            case "item.title-changed":
                if (Items.TryGetValue(d.ItemId!, out var t)) { var blob = ContentFormat.BlobIdOf(eventId); t.Title = d.Title; t.TitleBlob = blob; Own(blob, t, seq, ref t.History); }
                break;
            case "item.note-changed":
                if (Items.TryGetValue(d.ItemId!, out var n)) { var blob = ContentFormat.BlobIdOf(eventId); n.Note = d.Note; n.NoteBlob = blob; Own(blob, n, seq, ref n.History); }
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
                Relations[d.RelationId!] = new CRelation { Id = d.RelationId!, From = d.FromItemId!, TypeId = d.TypeId!, To = d.ToItemId!, Note = "" };
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
                if (Relations.TryGetValue(d.RelationId!, out var rn))
                {
                    var blob = ContentFormat.BlobIdOf(eventId);
                    rn.Note = d.Note; rn.NoteBlob = blob;
                    _blobOwner[blob] = rn;
                    if (TrackHistory) (rn.History ??= new()).Add((blob, seq));
                }
                break;
            case "relation-type.set":
                if (Items.TryGetValue(d.ItemId!, out var ty))
                {
                    var blob = ContentFormat.BlobIdOf(eventId);
                    ty.Symmetric = d.Symmetric; ty.ReverseName = d.ReverseName; ty.ReverseNameBlob = blob;
                    Own(blob, ty, seq, ref ty.History);
                }
                break;
            case "tree.inbox-set": InboxId = d.ItemId; break;
            case "tree.tags-set": TagsBranchId = d.ItemId; break;
            case "tree.relation-types-set": RelationTypesBranchId = d.ItemId; break;
            case "tree.default-relation-type-set": DefaultRelationTypeId = d.ItemId; break;
            case "device.joined":
            {
                var blob = ContentFormat.BlobIdOf(eventId);
                Devices[d.DeviceId!] = d.DeviceLabel;
                _deviceLabelBlob[d.DeviceId!] = blob;
                _blobOwner[blob] = d.DeviceId!;
                break;
            }
        }
    }

    private void Own(string blob, CItem item, int seq, ref List<(string, int)>? history)
    {
        _blobOwner[blob] = item;
        if (TrackHistory) (history ??= new()).Add((blob, seq));
    }

    /// <summary>Joins a blob that arrives after its event was applied (case 4); applies only if it is still the current value.</summary>
    public void ApplyContent(string blobId, Content c)
    {
        if (!_blobOwner.TryGetValue(blobId, out var owner)) return;
        switch (owner)
        {
            case CItem it:
                if (it.TitleBlob == blobId && c.Title != null) it.Title = c.Title;
                if (it.NoteBlob == blobId) it.Note = c.Note ?? "";
                if (it.ReverseNameBlob == blobId) it.ReverseName = c.ReverseName;
                break;
            case CRelation r:
                if (r.NoteBlob == blobId) r.Note = c.Note ?? "";
                break;
            case string deviceId:
                if (_deviceLabelBlob.GetValueOrDefault(deviceId) == blobId) Devices[deviceId] = c.DeviceLabel;
                break;
        }
    }

    private static string? Empty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    private List<string> Siblings(string? parentId) => parentId == null ? TopLevel : (Items[parentId].Children ??= new List<string>());

    private void Place(CItem item, string? afterItemId)
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

    public IEnumerable<CItem> VisibleTopLevel() => TopLevel.Select(id => Items[id]).Where(i => !i.Deleted);

    /// <summary>The item and all its descendants, archived ones included (ADR-091: erase takes the whole branch).</summary>
    public List<CItem> Branch(string itemId)
    {
        var list = new List<CItem>();
        var stack = new Stack<string>();
        stack.Push(itemId);
        while (stack.Count > 0)
        {
            var it = Items[stack.Pop()];
            list.Add(it);
            if (it.Children != null) foreach (var c in it.Children) stack.Push(c);
        }
        return list;
    }

    /// <summary>Every blob an erase of the branch removes: all text events of its items, older values included, and the notes of their relations.</summary>
    public List<(string BlobId, int Seq)> ErasableBlobs(string itemId)
    {
        var branch = Branch(itemId);
        var ids = new HashSet<string>(branch.Select(i => i.Id), StringComparer.Ordinal);
        var blobs = new List<(string, int)>();
        foreach (var it in branch) if (it.History != null) blobs.AddRange(it.History);
        foreach (var r in Relations.Values) if (r.History != null && (ids.Contains(r.From) || ids.Contains(r.To))) blobs.AddRange(r.History);
        return blobs;
    }

    /// <summary>Spike 1's checksum over the visible outline and live relations; text not loaded counts as empty.</summary>
    public string Checksum()
    {
        var sb = new StringBuilder();
        void Walk(List<string> ids, int depth)
        {
            foreach (var id in ids)
            {
                var it = Items[id];
                if (it.Deleted) continue;
                sb.Append(depth).Append('|').Append(id).Append('|').Append(it.Title ?? "").Append('|').Append(it.Note ?? "").Append('|');
                if (it.Tags != null) sb.AppendJoin(',', it.Tags.Order(StringComparer.Ordinal));
                sb.Append('\n');
                if (it.Children != null) Walk(it.Children, depth + 1);
            }
        }
        Walk(TopLevel, 0);
        foreach (var r in Relations.Values.Where(r => !r.Removed).OrderBy(r => r.Id, StringComparer.Ordinal))
            sb.Append("R|").Append(r.Id).Append('|').Append(r.From).Append('|').Append(r.TypeId).Append('|').Append(r.To).Append('|').Append(r.Note ?? "").Append('\n');
        sb.Append("M|").Append(InboxId).Append('|').Append(TagsBranchId).Append('|').Append(RelationTypesBranchId).Append('|').Append(DefaultRelationTypeId);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
