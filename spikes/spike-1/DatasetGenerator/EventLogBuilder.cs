using SpikeCore;

namespace DatasetGenerator;

/// <summary>
/// Builds a deterministic event log in the current format (architecture.md "Event types"; ADR-082 tags are items,
/// ADR-083 relation ids, ADR-084 relation types are items; ADR-066 inbox). Events are written in the agreed order
/// (increasing clock), so a replay may apply them as read.
/// </summary>
public sealed class EventLogBuilder
{
    public const long StartTimeMs = 1798761600000; // 2027-01-01T00:00:00Z, fake clock
    private static readonly TimeSpan Offset = TimeSpan.FromHours(1);

    private readonly SplitMix64 _rng;
    private readonly TextSource _text;
    private readonly uint _idPrefix;
    private readonly Dictionary<int, ulong> _counters = new();
    private long _clock = StartTimeMs;
    private string _device = "";
    private string _action = "";

    public readonly List<(string EventId, EventRecord Event)> Events = new();
    public readonly Projection Shadow = new();
    public readonly string[] DeviceIds;
    public readonly string UserId;

    private readonly List<string> _content = new();
    private readonly List<string> _tags = new();
    private readonly List<string> _types = new();
    private readonly Dictionary<string, string> _lastTitle = new(), _lastNote = new(), _lastMove = new(), _lastRelType = new(), _lastRelNote = new();
    private readonly List<string> _liveRelations = new(), _removedRelations = new();
    private readonly List<(string Item, string Tag)> _assigned = new();
    private readonly Dictionary<string, Clock> _seen = new();

    public EventLogBuilder(SplitMix64 rng, uint idPrefix)
    {
        _rng = rng;
        _idPrefix = idPrefix;
        _text = new TextSource(rng);
        DeviceIds = [NewId(5), NewId(5), NewId(5)];
        UserId = NewId(6);
    }

    public string NewId(int kind)
    {
        _counters.TryGetValue(kind, out var n);
        _counters[kind] = ++n;
        return $"{_idPrefix:x8}-{kind:x4}-4{(n >> 48) & 0xfff:x3}-8000-{n & 0xffffffffffffUL:x12}";
    }

    private void BeginAction(string? device = null)
    {
        _device = device ?? DeviceIds[_rng.Below(DeviceIds.Length)];
        _action = NewId(3);
    }

    private string Emit(string type, EventData data, string? reverts = null)
    {
        _clock += 1 + _rng.Below(90_000);
        var clock = new Clock { Time = _clock, Counter = 0 };
        var e = new EventRecord
        {
            Type = type, Schema = 1, DeviceId = _device, ActionId = _action, Clock = clock, Reverts = reverts,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(_clock).ToOffset(Offset).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture),
            Data = data,
        };
        var id = NewId(1);
        Events.Add((id, e));
        _seen[_device] = clock;
        Shadow.Apply(e);
        return id;
    }

    private string CreateItem(string? parent, string? after, string title, string note)
    {
        var id = NewId(2);
        var ev = Emit("item.created", new EventData { ItemId = id, ParentId = parent ?? "", AfterItemId = after ?? "", Title = title, Note = note.Length == 0 ? null : note, Format = "markdown" });
        _lastTitle[id] = ev; _lastNote[id] = ev; _lastMove[id] = ev;
        return id;
    }

    private string? LastChild(string? parent)
    {
        var list = parent == null ? Shadow.TopLevel : Shadow.Items[parent].Children;
        return list == null || list.Count == 0 ? null : list[^1];
    }

    private string? RandomChild(string? parent)
    {
        var list = parent == null ? Shadow.TopLevel : Shadow.Items[parent].Children;
        return list == null || list.Count == 0 ? null : list[_rng.Below(list.Count)];
    }

    /// <summary>Q5: 1,000 events; 100 items, each given one note of <paramref name="noteChars"/> characters; the rest title edits.</summary>
    public Dictionary<string, int> BuildLongNotes(int noteChars)
    {
        BeginAction(DeviceIds[0]);
        var inbox = CreateItem(null, null, "Inbox", "");
        Emit("tree.inbox-set", new EventData { ItemId = inbox });
        for (int i = 0; i < 100; i++) { BeginAction(); _content.Add(CreateItem(null, i == 0 ? inbox : _content[^1], _text.Title(), "")); }
        foreach (var id in _content) { BeginAction(); _lastNote[id] = Emit("item.note-changed", new EventData { ItemId = id, Note = _text.Text(noteChars), Format = "markdown", Replaces = [_lastNote[id]] }); }
        while (Events.Count < 1000) { BeginAction(); var id = _rng.Pick(_content); _lastTitle[id] = Emit("item.title-changed", new EventData { ItemId = id, Title = _text.Title(), Format = "markdown", Replaces = [_lastTitle[id]] }); }
        return Events.GroupBy(e => e.Event.Type).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());
    }

    public Dictionary<string, int> Build(int totalEvents)
    {
        int items = totalEvents / 10;
        int tagItems = Math.Max(5, totalEvents / 670);
        int typeItems = Math.Max(2, totalEvents / 20000 + 1);           // includes "related"
        int structural = 3;                                              // inbox, Tags, Relation types
        int contentItems = items - structural - tagItems - typeItems;
        int deviceJoins = DeviceIds.Length, markers = 4, typeSets = typeItems;
        int moves = totalEvents * 12 / 100;
        int deletePairs = totalEvents * 5 / 200;
        int tagEvents = totalEvents * 6 / 100;
        int relationEvents = totalEvents * 5 / 100;
        int notes = totalEvents * 16 / 100;
        int titles = totalEvents - items - moves - 2 * deletePairs - tagEvents - relationEvents - notes - deviceJoins - markers - typeSets;

        // Tree creation: one action (ADR-066, ADR-082, ADR-084), then the devices join.
        BeginAction(DeviceIds[0]);
        var inbox = CreateItem(null, null, "Inbox", "");
        var tagsBranch = CreateItem(null, inbox, "Tags", "");
        var typesBranch = CreateItem(null, tagsBranch, "Relation types", "");
        var related = CreateItem(typesBranch, null, "related", "");
        _types.Add(related);
        Emit("tree.inbox-set", new EventData { ItemId = inbox });
        Emit("tree.tags-set", new EventData { ItemId = tagsBranch });
        Emit("tree.relation-types-set", new EventData { ItemId = typesBranch });
        Emit("tree.default-relation-type-set", new EventData { ItemId = related });
        Emit("relation-type.set", new EventData { ItemId = related, Symmetric = true, ReverseName = "", Replaces = [] });
        for (int i = 0; i < DeviceIds.Length; i++)
        {
            BeginAction(DeviceIds[i]);
            Emit("device.joined", new EventData { DeviceId = DeviceIds[i], DeviceLabel = $"Test device {i + 1}", UserId = UserId, UserName = "Test user" });
        }

        int createsLeft = items - 4, tagsLeft = tagItems, typesLeft = typeItems - 1, contentLeft = contentItems;
        var remaining = new int[] { createsLeft, titles, notes, moves, deletePairs, tagEvents, relationEvents };
        var pendingRestores = new List<(int Due, string Item, string DeleteEvent)>();
        int step = 0;

        while (remaining.Sum() > 0 || pendingRestores.Count > 0)
        {
            step++;
            var due = pendingRestores.FindIndex(r => r.Due <= step || remaining.Sum() == 0);
            if (due >= 0)
            {
                var r = pendingRestores[due];
                pendingRestores.RemoveAt(due);
                BeginAction();
                Emit("item.restored", new EventData { ItemId = r.Item }, reverts: r.DeleteEvent);
                continue;
            }
            int total = remaining.Sum();
            int pick = _rng.Below(total), cat = 0;
            while (pick >= remaining[cat]) { pick -= remaining[cat]; cat++; }
            if (_content.Count < 3 && cat != 0 && remaining[0] > 0) cat = 0;
            if (cat == 5 && _tags.Count == 0 && remaining[0] > 0) cat = 0;
            remaining[cat]--;
            BeginAction();

            switch (cat)
            {
                case 0: // create
                {
                    int left = tagsLeft + typesLeft + contentLeft;
                    int k = _rng.Below(left);
                    if (k < tagsLeft)
                    {
                        tagsLeft--;
                        string parent = _tags.Count > 3 && _rng.Chance(0.3) ? _rng.Pick(_tags) : tagsBranch;
                        _tags.Add(CreateItem(parent, LastChild(parent), _text.Word(), _rng.Chance(0.3) ? _text.Text(60 + _rng.Below(200)) : ""));
                    }
                    else if (k < tagsLeft + typesLeft)
                    {
                        typesLeft--;
                        var type = CreateItem(typesBranch, LastChild(typesBranch), _text.Word(), _text.Text(40 + _rng.Below(120)));
                        _types.Add(type);
                        // the type's properties are part of the same action (counted in typeSets)
                        Emit("relation-type.set", new EventData { ItemId = type, Symmetric = _rng.Chance(0.5), ReverseName = _text.Word(), Replaces = [] });
                    }
                    else
                    {
                        contentLeft--;
                        string? parent;
                        double r = _rng.NextDouble();
                        if (_content.Count == 0 || r < 0.08) parent = null;
                        else if (r < 0.13) parent = inbox;
                        else if (r < 0.55) parent = _content[Math.Max(0, _content.Count - 1 - _rng.Below(Math.Min(200, _content.Count)))];
                        else parent = _rng.Pick(_content);
                        string? after = _rng.Chance(0.3) ? null : (_rng.Chance(0.7) ? LastChild(parent) : RandomChild(parent));
                        _content.Add(CreateItem(parent, after, _text.Title(), _rng.Chance(0.4) ? _text.Note() : ""));
                    }
                    break;
                }
                case 1: // title
                {
                    string id = _rng.Chance(0.97) ? _rng.Pick(_content) : (_rng.Chance(0.7) && _tags.Count > 0 ? _rng.Pick(_tags) : _rng.Pick(_types));
                    _lastTitle[id] = Emit("item.title-changed", new EventData { ItemId = id, Title = _text.Title(), Format = "markdown", Replaces = [_lastTitle[id]] });
                    break;
                }
                case 2: // note
                {
                    string id = _rng.Pick(_content);
                    _lastNote[id] = Emit("item.note-changed", new EventData { ItemId = id, Note = _text.Note(), Format = "markdown", Replaces = [_lastNote[id]] });
                    break;
                }
                case 3: // move
                {
                    string id = _rng.Pick(_content);
                    string? parent = _rng.Chance(0.1) ? null : (_rng.Chance(0.05) ? inbox : _rng.Pick(_content));
                    if (parent == id) parent = null;
                    string? after = _rng.Chance(0.5) ? null : RandomChild(parent);
                    if (after == id) after = null;
                    _lastMove[id] = Emit("item.moved", new EventData { ItemId = id, ParentId = parent ?? "", AfterItemId = after ?? "", Replaces = [_lastMove[id]] });
                    break;
                }
                case 4: // delete, restored later by undo
                {
                    string id = _rng.Pick(_content);
                    if (Shadow.Items[id].Deleted) { remaining[4]++; break; }
                    var seen = _seen.ToDictionary(kv => kv.Key, kv => new Clock { Time = kv.Value.Time, Counter = kv.Value.Counter });
                    var del = Emit("item.deleted", new EventData { ItemId = id, Seen = seen });
                    pendingRestores.Add((step + 1 + _rng.Below(40), id, del));
                    break;
                }
                case 5: // tag assignment
                {
                    if (_assigned.Count > 0 && _rng.Chance(0.2))
                    {
                        int i = _rng.Below(_assigned.Count);
                        var (item, tag) = _assigned[i];
                        _assigned.RemoveAt(i);
                        Emit("item.untagged", new EventData { ItemId = item, TagId = tag });
                    }
                    else
                    {
                        var item = _rng.Pick(_content); var tag = _rng.Pick(_tags);
                        _assigned.Add((item, tag));
                        Emit("item.tagged", new EventData { ItemId = item, TagId = tag });
                    }
                    break;
                }
                case 6: // relations (ADR-083)
                {
                    double r = _rng.NextDouble();
                    if (_liveRelations.Count == 0 || r < 0.60)
                    {
                        var rel = NewId(4);
                        var type = _rng.Chance(0.7) ? _types[0] : _rng.Pick(_types);
                        var ev = Emit("relation.added", new EventData { RelationId = rel, FromItemId = _rng.Pick(_content), TypeId = type, ToItemId = _rng.Pick(_content) });
                        _liveRelations.Add(rel); _lastRelType[rel] = ev;
                    }
                    else if (r < 0.75)
                    {
                        int i = _rng.Below(_liveRelations.Count); var rel = _liveRelations[i];
                        _liveRelations.RemoveAt(i); _removedRelations.Add(rel);
                        Emit("relation.removed", new EventData { RelationId = rel });
                    }
                    else if (r < 0.80 && _removedRelations.Count > 0)
                    {
                        int i = _rng.Below(_removedRelations.Count); var rel = _removedRelations[i];
                        _removedRelations.RemoveAt(i); _liveRelations.Add(rel);
                        Emit("relation.restored", new EventData { RelationId = rel });
                    }
                    else if (r < 0.90)
                    {
                        var rel = _rng.Pick(_liveRelations);
                        _lastRelType[rel] = Emit("relation.type-changed", new EventData { RelationId = rel, TypeId = _rng.Pick(_types), Replaces = [_lastRelType[rel]] });
                    }
                    else
                    {
                        var rel = _rng.Pick(_liveRelations);
                        var ev = Emit("relation.note-changed", new EventData { RelationId = rel, Note = _text.Text(20 + _rng.Below(200)), Format = "markdown", Replaces = _lastRelNote.TryGetValue(rel, out var prev) ? [prev] : [] });
                        _lastRelNote[rel] = ev;
                    }
                    break;
                }
            }
        }

        return Events.GroupBy(e => e.Event.Type).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());
    }
}
