using System.Text.RegularExpressions;

namespace ThoughtTree.Architecture.Rules.Support;

/// <summary>One command line of an ADR's verify block (docs/adr/README.md, Format).</summary>
internal sealed partial record VerifyLine(string Adr, string Command, string Comment)
{
    /// <summary>The ADR named in <c>dotnet test --filter "ADR=ADR-0xx"</c>, or null for other commands.</summary>
    public string? FilterAdr => FilterPattern().Match(Command) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>The loop the line waits for (after deployment, release, server), wherever "when:" appears in the comment.</summary>
    public string? When => WhenPattern().Match(Comment) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>True when the comment starts with "when:", the form docs/adr/README.md uses.</summary>
    public bool HasCanonicalWhen => CanonicalWhenPattern().IsMatch(Comment);

    /// <summary>A check that only reads files: grep, negated grep or sha256sum.</summary>
    public bool IsStatic => StaticPattern().IsMatch(Command);

    /// <summary>The value after "expect", if the comment names one.</summary>
    public string? Expected => ExpectPattern().Match(Comment) is { Success: true } m ? m.Groups[1].Value : null;

    public override string ToString() => $"{Adr}: {Command}";

    [GeneratedRegex(@"^dotnet test --filter ""ADR=(ADR-\d{3})""$", RegexOptions.CultureInvariant)]
    private static partial Regex FilterPattern();

    [GeneratedRegex(@"\bwhen:\s*(after deployment|release|server)\b", RegexOptions.CultureInvariant)]
    private static partial Regex WhenPattern();

    [GeneratedRegex(@"^when:\s*(after deployment|release|server)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalWhenPattern();

    [GeneratedRegex(@"^(! )?(grep|sha256sum) ", RegexOptions.CultureInvariant)]
    private static partial Regex StaticPattern();

    [GeneratedRegex(@"^expect (\S+?)[:;,.]?(\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex ExpectPattern();
}

/// <summary>Reads the verify blocks of all ADRs.</summary>
internal static class AdrVerifyLines
{
    public static IReadOnlyList<VerifyLine> All { get; } = Load();

    private static List<VerifyLine> Load()
    {
        var lines = new List<VerifyLine>();
        foreach (var file in Directory.GetFiles(Repository.PathOf("docs", "adr"), "ADR-*.md").Order(StringComparer.Ordinal))
        {
            var adr = Path.GetFileName(file)[..7];
            var text = File.ReadAllLines(file);
            var start = Array.FindIndex(text, l => l.Trim() == "verify:");
            if (start < 0)
            {
                continue;
            }

            var open = Array.FindIndex(text, start, l => l.TrimStart().StartsWith("```", StringComparison.Ordinal));
            for (var i = open + 1; i < text.Length && !text[i].TrimStart().StartsWith("```", StringComparison.Ordinal); i++)
            {
                if (text[i].Trim().Length == 0)
                {
                    continue;
                }

                var (command, comment) = SplitComment(text[i]);
                lines.Add(new VerifyLine(adr, command, comment));
            }
        }

        return lines;
    }

    /// <summary>Splits "command   # comment" at the first '#' after whitespace that is outside quotes.</summary>
    internal static (string Command, string Comment) SplitComment(string line)
    {
        char? quote = null;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote is null && (c == '\'' || c == '"'))
            {
                quote = c;
            }
            else if (quote == c)
            {
                quote = null;
            }
            else if (quote is null && c == '#' && i > 0 && char.IsWhiteSpace(line[i - 1]))
            {
                return (line[..i].Trim(), line[(i + 1)..].Trim());
            }
        }

        return (line.Trim(), string.Empty);
    }
}
