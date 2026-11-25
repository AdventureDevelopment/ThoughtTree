using System.Text;
using System.Text.Json.Serialization;

namespace SpikeCore;

/// <summary>
/// Matching rules of architecture.md "Search": case ignored; accents ignored ("Bar" finds "Bär", "cafe" finds "café");
/// German equivalents (ä = ae, ö = oe, ü = ue, ß = ss); Markdown markers ignored; emojis searchable.
/// Index side: a token with an umlaut is stored under its accent-free form and its German expansion.
/// </summary>
public static class Normaliser
{
    private const string From = "äöüàáâãåāăąçćĉċčďđèéêëēĕėęěĝğġģĥħìíîïĩīĭįıĵķĺļľŀłñńņňòóôõøōŏőŕŗřśŝşšţťŧùúûũůūŭűųŵýÿŷźżžæœ";
    private const string To   = "aouaaaaaaaacccccddeeeeeeeeegggghhiiiiiiiiijklllllnnnnoooooooorrrssssttttuuuuuuuuuwyyyzzzææ";

    private static char Fold(char c)
    {
        int i = From.IndexOf(c);
        return i < 0 ? c : To[i];
    }

    /// <summary>Splits text into lower-case tokens; letters and digits form words, every symbol (emoji) is its own token.</summary>
    public static void Tokens(string text, List<string> into)
    {
        var sb = new StringBuilder();
        void Flush() { if (sb.Length > 0) { into.Add(sb.ToString()); sb.Clear(); } }
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length)
            {
                Flush(); into.Add(text.Substring(i, 2)); i++; continue;
            }
            if (char.IsLetterOrDigit(c)) { sb.Append(char.ToLowerInvariant(c)); continue; }
            Flush();
            var cat = char.GetUnicodeCategory(c);
            if (cat is System.Globalization.UnicodeCategory.OtherSymbol) into.Add(c.ToString());
        }
        Flush();
    }

    /// <summary>Keys under which a token is indexed (1 or 2).</summary>
    public static void IndexKeys(string token, List<string> into)
    {
        var plain = new StringBuilder(token.Length);
        StringBuilder? german = null;
        foreach (var c in token)
        {
            switch (c)
            {
                case 'ß': plain.Append("ss"); german?.Append("ss"); break;
                case 'ä': case 'ö': case 'ü':
                    german ??= new StringBuilder(plain.ToString());
                    plain.Append(c == 'ä' ? 'a' : c == 'ö' ? 'o' : 'u');
                    german.Append(c == 'ä' ? "ae" : c == 'ö' ? "oe" : "ue");
                    break;
                default: var f = Fold(c); plain.Append(f); german?.Append(f); break;
            }
        }
        into.Add(plain.ToString());
        if (german != null) into.Add(german.ToString());
    }

    /// <summary>Key of a query token: accent-free, ß as ss ("bär" → "bar", "baer" stays "baer").</summary>
    public static string QueryKey(string token)
    {
        var sb = new StringBuilder(token.Length);
        foreach (var c in token) { if (c == 'ß') sb.Append("ss"); else sb.Append(Fold(c)); }
        return sb.ToString();
    }
}

/// <summary>Inverted index of titles and notes; the same code builds variants A, B and C (spike plan Q4).</summary>
public sealed class SearchIndex
{
    private readonly Dictionary<string, List<int>> _postings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _ordinal = new(StringComparer.Ordinal);
    private readonly List<string> _ids = new();
    private string[]? _sortedKeys;
    [ThreadStatic] private static List<string>? _tmpTokens;
    [ThreadStatic] private static List<string>? _tmpKeys;

    public int TermCount => _postings.Count;
    public long PostingCount { get { long n = 0; foreach (var p in _postings.Values) n += p.Count; return n; } }

    public static SearchIndex Build(Projection p)
    {
        var ix = new SearchIndex();
        foreach (var it in p.Items.Values) ix.Add(it.Id, it.Title, it.Note);
        return ix;
    }

    private int Ord(string id)
    {
        if (!_ordinal.TryGetValue(id, out var o)) { o = _ids.Count; _ids.Add(id); _ordinal[id] = o; }
        return o;
    }

    private static HashSet<string> Keys(string title, string note)
    {
        var tokens = _tmpTokens ??= new List<string>();
        var keys = _tmpKeys ??= new List<string>();
        tokens.Clear(); keys.Clear();
        Normaliser.Tokens(title, tokens);
        Normaliser.Tokens(note, tokens);
        foreach (var t in tokens) Normaliser.IndexKeys(t, keys);
        return new HashSet<string>(keys, StringComparer.Ordinal);
    }

    public void Add(string id, string title, string note)
    {
        int o = Ord(id);
        foreach (var k in Keys(title, note))
        {
            if (!_postings.TryGetValue(k, out var list)) { _postings[k] = list = new List<int>(2); _sortedKeys = null; }
            list.Add(o);
        }
    }

    /// <summary>Incremental update for one title or note change.</summary>
    public void Update(string id, string oldTitle, string oldNote, string newTitle, string newNote)
    {
        int o = Ord(id);
        var oldKeys = Keys(oldTitle, oldNote);
        var newKeys = Keys(newTitle, newNote);
        foreach (var k in oldKeys)
            if (!newKeys.Contains(k) && _postings.TryGetValue(k, out var l)) { l.Remove(o); if (l.Count == 0) { _postings.Remove(k); _sortedKeys = null; } }
        foreach (var k in newKeys)
            if (!oldKeys.Contains(k))
            {
                if (!_postings.TryGetValue(k, out var l)) { _postings[k] = l = new List<int>(2); _sortedKeys = null; }
                l.Add(o);
            }
    }

    /// <summary>All query words must match; the last word also matches as a prefix. Returns up to <paramref name="limit"/> visible items.</summary>
    public List<string> Query(string query, Projection p, int limit = 50)
    {
        var tokens = new List<string>();
        Normaliser.Tokens(query, tokens);
        if (tokens.Count == 0) return new();
        HashSet<int>? acc = null;
        for (int i = 0; i < tokens.Count; i++)
        {
            var key = Normaliser.QueryKey(tokens[i]);
            var set = new HashSet<int>();
            if (_postings.TryGetValue(key, out var exact)) set.UnionWith(exact);
            if (i == tokens.Count - 1)
            {
                var keys = _sortedKeys ??= _postings.Keys.Order(StringComparer.Ordinal).ToArray();
                int lo = Array.BinarySearch(keys, key, StringComparer.Ordinal);
                if (lo < 0) lo = ~lo;
                for (int j = lo; j < keys.Length && keys[j].StartsWith(key, StringComparison.Ordinal); j++) set.UnionWith(_postings[keys[j]]);
            }
            if (acc == null) acc = set; else acc.IntersectWith(set);
            if (acc.Count == 0) return new();
        }
        var hits = new List<string>();
        foreach (var o in acc!.Order())
        {
            var id = _ids[o];
            if (p.Items.TryGetValue(id, out var it) && !it.Deleted) { hits.Add(id); if (hits.Count >= limit) break; }
        }
        return hits;
    }

    // ---- persistence (variants B and C) ----

    public List<IndexChunkDto> ToChunks(int termsPerChunk)
    {
        var chunks = new List<IndexChunkDto> { new() { Ids = _ids } };
        IndexChunkDto? cur = null;
        foreach (var kv in _postings)
        {
            if (cur == null || cur.Terms!.Count >= termsPerChunk) { cur = new IndexChunkDto { Terms = new(), Postings = new() }; chunks.Add(cur); }
            cur.Terms!.Add(kv.Key);
            cur.Postings!.Add(kv.Value);
        }
        return chunks;
    }

    public void Load(IndexChunkDto chunk)
    {
        if (chunk.Ids != null)
            foreach (var id in chunk.Ids) { _ordinal[id] = _ids.Count; _ids.Add(id); }
        if (chunk.Terms != null)
            for (int i = 0; i < chunk.Terms.Count; i++) _postings[chunk.Terms[i]] = chunk.Postings![i];
        _sortedKeys = null;
    }
}

public sealed class IndexChunkDto
{
    [JsonPropertyName("ids")] public List<string>? Ids { get; set; }
    [JsonPropertyName("terms")] public List<string>? Terms { get; set; }
    [JsonPropertyName("postings")] public List<List<int>>? Postings { get; set; }
}
