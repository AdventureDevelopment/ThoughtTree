using System.Buffers;
using System.Text;

namespace ThoughtTree.Domain.Outline;

/// <summary>An item's title, single-line and in Unicode NFC whatever it was built from (ADR-029, INV-11).</summary>
public readonly record struct Title
{
    private readonly string? value;

    public Title(string value)
    {
        this.value = NormalisedText.ForTitle(value);
    }

    public string Value => this.value ?? "";

    public static implicit operator Title(string value) => new(value);

    public bool Equals(Title other) => string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Value);

    public override string ToString() => this.Value;
}

/// <summary>An item's note, in Unicode NFC with every line break stored as a line feed (ADR-029, INV-11).</summary>
public readonly record struct Note
{
    private readonly string? value;

    public Note(string value)
    {
        this.value = NormalisedText.ForNote(value);
    }

    public string Value => this.value ?? "";

    public static implicit operator Note(string value) => new(value);

    public bool Equals(Note other) => string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Value);

    public override string ToString() => this.Value;
}

internal static class NormalisedText
{
    private const string LineBreaks = "\r\n\u0085\u2028\u2029\u000B\u000C";

    private static readonly SearchValues<char> AnyLineBreak = SearchValues.Create(LineBreaks);

    private static readonly SearchValues<char> LineBreakOtherThanLineFeed = SearchValues.Create(LineBreaks.Replace("\n", "", StringComparison.Ordinal));

    public static string ForTitle(string text) => Composed(WithLineBreaksAs(text, ' ', AnyLineBreak));

    public static string ForNote(string text) => Composed(WithLineBreaksAs(text, '\n', LineBreakOtherThanLineFeed));

    private static string Composed(string text) => text.Normalize(NormalizationForm.FormC);

    private static string WithLineBreaksAs(string text, char lineBreak, SearchValues<char> toReplace)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.AsSpan().ContainsAny(toReplace))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (!AnyLineBreak.Contains(text[i]))
            {
                result.Append(text[i]);
                continue;
            }

            result.Append(lineBreak);
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }
        }

        return result.ToString();
    }
}
