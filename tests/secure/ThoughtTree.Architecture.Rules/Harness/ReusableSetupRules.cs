using System.Text.RegularExpressions;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Harness;

/// <summary>The reusable setup listed in eng/reusable-setup.txt stays free of the project's domain terms (AGENTS.md section 4).</summary>
[Trait("Intent", "Secure")]
public sealed class ReusableSetupRules
{
    private const string ReusableSetup = "eng/reusable-setup.txt";
    private const string DomainTerms = "eng/domain-terms.txt";

    private static readonly string[] SkippedFolders = ["bin", "obj", "TestResults", "node_modules", "__pycache__"];

    [Fact]
    public void Every_listed_part_of_the_reusable_setup_exists()
    {
        EntriesOf(ReusableSetup).Where(path => !File.Exists(Repository.PathOf(path)) && !Directory.Exists(Repository.PathOf(path)))
            .ToList().ShouldBeEmpty($"remove or fix stale lines in {ReusableSetup}");
    }

    [Fact]
    public void No_part_of_the_reusable_setup_uses_a_domain_term()
    {
        var terms = TermPattern(EntriesOf(DomainTerms));
        var offenders = ReusableFiles()
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (File: file, Line: index + 1, Text: line)))
            .Where(numberedLine => terms.IsMatch(numberedLine.Text))
            .Select(numberedLine => $"{Repository.Relative(numberedLine.File)}:{numberedLine.Line}: {terms.Match(numberedLine.Text).Value}")
            .ToList();

        offenders.ShouldBeEmpty($"keep the reusable setup free of {DomainTerms}; move project-specific parts into their own files");
    }

    [Theory]
    [InlineData("var thing = new Thing();", false)]
    [InlineData("var widget = new Widget(id);", true)]
    [InlineData("\"Widgets\": []", false)]
    public void Domain_terms_match_whole_words_only(string text, bool found)
    {
        TermPattern(["Widget"]).IsMatch(text).ShouldBe(found);
    }

    private static Regex TermPattern(IEnumerable<string> terms) =>
        new($@"\b({string.Join('|', terms.Select(Regex.Escape))})\b", RegexOptions.CultureInvariant);

    private static IEnumerable<string> EntriesOf(string list) =>
        File.ReadAllLines(Repository.PathOf(list)).Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'));

    private static IEnumerable<string> ReusableFiles() =>
        EntriesOf(ReusableSetup).SelectMany(entry => entry.EndsWith('/')
            ? Directory.EnumerateFiles(Repository.PathOf(entry), "*", SearchOption.AllDirectories).Where(file => !IsSkipped(file))
            : [Repository.PathOf(entry)]);

    private static bool IsSkipped(string path) => Repository.Relative(path).Split('/').Intersect(SkippedFolders).Any();
}
