using System.Text;
using SpikeCore;

namespace DatasetGenerator;

/// <summary>Deterministic German and English text with accents, emoji and a few Markdown markers.</summary>
public sealed class TextSource
{
    private static readonly string[] Common =
    [
        "und", "der", "die", "das", "nicht", "mit", "für", "über", "Bär", "Käse", "Grüße", "Straße", "Müller", "schön", "Übung", "Ärger", "Öl",
        "Idee", "Gedanke", "Projekt", "Notiz", "Baum", "Zweig", "Entwurf", "Frage", "Antwort", "Möglichkeit", "Größe", "weiß", "heißt", "fließen",
        "the", "and", "of", "to", "idea", "thought", "draft", "plan", "meeting", "review", "question", "answer", "tree", "branch", "note", "café",
        "naïve", "résumé", "façade", "jalapeño", "crème", "brûlée", "São", "Zürich", "Genève", "Ångström", "Œuvre", "déjà", "vu", "coöperate",
        "Zettelkasten", "outline", "search", "garden", "project", "Aufgabe", "Termin", "Kunde", "Rechnung", "Reise", "Buch", "Kapitel", "Quelle",
    ];
    private static readonly string[] Emoji = ["🌳", "💡", "✅", "🚀", "📌", "❤️", "🙂", "🔥", "📚", "🧠"];
    private static readonly string[] Syllables =
    [
        "ka", "lo", "mi", "ter", "ber", "schl", "un", "ge", "ver", "ent", "ion", "ing", "ma", "ro", "sti", "pha", "ün", "ä", "ö", "zu",
        "an", "pre", "con", "tra", "del", "fi", "nu", "bel", "gar", "hol", "wen", "dor", "lie", "sch", "tsch", "é", "ço", "ri", "pa", "xe",
    ];

    private readonly SplitMix64 _rng;
    private readonly string[] _vocab;

    public TextSource(SplitMix64 rng, int vocabularySize = 20000)
    {
        _rng = rng;
        var set = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>(Common);
        foreach (var w in Common) set.Add(w);
        while (list.Count < vocabularySize)
        {
            int n = 2 + _rng.Below(3);
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++) sb.Append(Syllables[_rng.Below(Syllables.Length)]);
            var w = sb.ToString();
            if (set.Add(w)) list.Add(w);
        }
        _vocab = list.ToArray();
    }

    /// <summary>Zipf-like word: log-uniform rank.</summary>
    public string Word()
    {
        double u = _rng.NextDouble();
        int rank = (int)Math.Exp(u * Math.Log(_vocab.Length));
        return _vocab[Math.Clamp(rank - 1, 0, _vocab.Length - 1)];
    }

    public string Title()
    {
        int n = 1 + _rng.Below(7);
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(' ');
            var w = Word();
            sb.Append(i == 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w);
        }
        if (_rng.Chance(0.05)) sb.Append(' ').Append(Emoji[_rng.Below(Emoji.Length)]);
        if (_rng.Chance(0.05)) { sb.Insert(0, "**"); sb.Append("**"); }
        return sb.ToString();
    }

    /// <summary>Note length: mostly short, a long tail up to 4,000 characters (spike plan section 2).</summary>
    public int NoteLength()
    {
        double u = _rng.NextDouble();
        if (u < 0.50) return _rng.Below(81);
        if (u < 0.80) return 80 + _rng.Below(321);
        if (u < 0.95) return 400 + _rng.Below(1101);
        return 1500 + _rng.Below(2501);
    }

    public string Text(int length)
    {
        if (length == 0) return "";
        var sb = new StringBuilder(length + 16);
        while (sb.Length < length)
        {
            if (sb.Length > 0) sb.Append(_rng.Chance(0.08) ? ".\n" : " ");
            double r = _rng.NextDouble();
            if (r < 0.01) sb.Append("- ");
            else if (r < 0.02) sb.Append(Emoji[_rng.Below(Emoji.Length)]).Append(' ');
            else if (r < 0.025) sb.Append('[').Append(Word()).Append("](https://example.org/").Append(Word()).Append(')');
            sb.Append(Word());
        }
        if (sb.Length > length)
        {
            int cut = length;
            if (char.IsHighSurrogate(sb[cut - 1])) cut--;
            sb.Length = cut;
        }
        return sb.ToString();
    }

    /// <summary>A text of exactly <paramref name="length"/> UTF-16 characters or one less (no split surrogate pair).</summary>
    public string Note() => Text(NoteLength());
}
