using System.Buffers;
using System.Text;

namespace ThoughtTree.Domain.Outline;

/// <summary>Brings raw text into the stored form of a title or a note (ADR-029, INV-11).</summary>
internal static class NormalisedText
{
    private const char Replacement = '\uFFFD';

    private const char FirstSurrogate = '\uD800';

    private const char LastSurrogate = '\uDFFF';

    private const string LineBreaks = "\r\n\u0085\u2028\u2029\u000B\u000C";

    private static readonly SearchValues<char> AnyLineBreak = SearchValues.Create(LineBreaks);

    private static readonly SearchValues<char> LineBreakOtherThanLineFeed = SearchValues.Create(LineBreaks.Replace("\n", "", StringComparison.Ordinal));

    public static string ForTitle(string text) => Composed(WithLineBreaksAs(WithoutUnpairedSurrogates(text), ' ', AnyLineBreak));

    public static string ForNote(string text) => Composed(WithLineBreaksAs(WithoutUnpairedSurrogates(text), '\n', LineBreakOtherThanLineFeed));

    private static string Composed(string text) => text.Normalize(NormalizationForm.FormC);

    private static string WithoutUnpairedSurrogates(string text)
    {
        if (text.AsSpan().IndexOfAnyInRange(FirstSurrogate, LastSurrogate) == -1)
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                result.Append(text[i]).Append(text[i + 1]);
                i++;
            }
            else
            {
                result.Append(char.IsSurrogate(text[i]) ? Replacement : text[i]);
            }
        }

        return result.ToString();
    }

    private static string WithLineBreaksAs(string text, char lineBreak, SearchValues<char> needsWork)
    {
        if (!text.AsSpan().ContainsAny(needsWork))
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
