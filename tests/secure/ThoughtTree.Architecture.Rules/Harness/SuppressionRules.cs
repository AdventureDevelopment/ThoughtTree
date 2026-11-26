using System.Text.RegularExpressions;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Harness;

/// <summary>
/// ADR-042: analyzer and architecture rules are suppressed only in the files listed in
/// eng/approved-suppressions.txt. Severity changes and MSBuild switches count as suppressions too.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-042")]
public sealed partial class SuppressionRules
{
    private static readonly string[] ScannedPatterns = ["*.cs", "*.razor", "*.csproj", "*.props", "*.targets", ".editorconfig", "*.globalconfig"];

    private static readonly string[] ScannedFolders = ["src", "tests", "eng"];

    [Fact]
    public void No_suppression_outside_the_approved_files()
    {
        var approved = ApprovedFiles();
        var offenders = ScannedFiles()
            .Where(f => !approved.Contains(Repository.Relative(f)))
            .SelectMany(f => Suppressions(Repository.Relative(f), File.ReadAllText(f)))
            .ToList();

        offenders.ShouldBeEmpty("ADR-042: suppress only in files listed in eng/approved-suppressions.txt");
    }

    [Fact]
    public void Every_approved_file_exists_and_names_a_reason()
    {
        foreach (var line in ApprovedLines())
        {
            var parts = line.Split('#', 2);
            File.Exists(Repository.PathOf(parts[0].Trim())).ShouldBeTrue($"approved file '{parts[0].Trim()}' does not exist");
            parts.ElementAtOrDefault(1).ShouldNotBeNullOrWhiteSpace($"'{line}' needs a reason after '#'");
        }
    }

    // The samples are assembled from pieces so that this file does not contain a suppression itself.
    [Theory]
    [InlineData("A.cs", "#" + "pragma warning disable RS0030")]
    [InlineData("A.cs", "  #  " + "pragma warning disable")]
    [InlineData("A.cs", "[assembly: " + "SuppressMessage(\"x\", \"CA1000\")]")]
    [InlineData("A.cs", "[System.Diagnostics.CodeAnalysis." + "SuppressMessage(\"x\", \"y\")]")]
    [InlineData("A.razor", "@{ #" + "pragma warning disable CS0168 }")]
    [InlineData("A.cs", "[Unconditional" + "SuppressMessage(\"x\", \"y\")]")]
    [InlineData(".editorconfig", "dotnet_diagnostic.RS0030." + "severity = none")]
    [InlineData("x/.globalconfig", "dotnet_diagnostic.CA2000." + "severity=suggestion")]
    [InlineData(".editorconfig", "dotnet_analyzer_diagnostic.category-Security." + "severity = silent")]
    [InlineData("A.csproj", "<" + "NoWarn>RS0030</NoWarn>")]
    [InlineData("A.props", "<" + "WarningsNotAsErrors>CA1000</WarningsNotAsErrors>")]
    [InlineData("A.targets", "<TreatWarningsAsErrors>" + "false</TreatWarningsAsErrors>")]
    [InlineData("A.csproj", "<RunAnalyzers>" + "false</RunAnalyzers>")]
    [InlineData("A.csproj", "<EnableNETAnalyzers>" + "false</EnableNETAnalyzers>")]
    [InlineData("A.csproj", "<PackageReference Include=\"X\" " + "NoWarn=\"NU1608\" />")]
    public void Each_kind_of_suppression_is_found(string file, string text)
    {
        Suppressions(file, text).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("A.cs", "#pragma warning restore CA1000")]
    [InlineData(".editorconfig", "dotnet_diagnostic.CA1000.severity = error")]
    [InlineData("A.csproj", "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>")]
    public void Raising_or_restoring_a_rule_is_not_a_suppression(string file, string text)
    {
        Suppressions(file, text).ShouldBeEmpty();
    }

    internal static IEnumerable<string> Suppressions(string relativePath, string text)
    {
        var name = Path.GetFileName(relativePath);
        var pattern = Path.GetExtension(relativePath) switch
        {
            ".cs" or ".razor" => CodeSuppression(),
            ".csproj" or ".props" or ".targets" => MsBuildSuppression(),
            _ when name is ".editorconfig" or ".globalconfig" || name.EndsWith(".globalconfig", StringComparison.Ordinal) => ConfigSuppression(),
            _ => null,
        };

        return pattern is null
            ? []
            : pattern.Matches(text).Select(m => $"{relativePath}: {m.Value.Trim()}");
    }

    private static IEnumerable<string> ScannedFiles()
    {
        var below = ScannedFolders.SelectMany(d => ScannedPatterns.SelectMany(p => Repository.Files(d, p)));
        var root = ScannedPatterns.SelectMany(p => Directory.EnumerateFiles(Repository.Root, p, SearchOption.TopDirectoryOnly));
        return below.Concat(root).Distinct().Order(StringComparer.Ordinal);
    }

    private static HashSet<string> ApprovedFiles() =>
        [.. ApprovedLines().Select(l => l.Split('#', 2)[0].Trim())];

    private static IEnumerable<string> ApprovedLines() =>
        File.ReadAllLines(Repository.PathOf("eng", "approved-suppressions.txt"))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'));

    [GeneratedRegex(@"#\s*pragma\s+warning\s+disable|\[\s*(assembly\s*:\s*|module\s*:\s*)?(global::)?(System\.Diagnostics\.CodeAnalysis\.)?(Unconditional)?SuppressMessage(Attribute)?\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex CodeSuppression();

    [GeneratedRegex(@"<NoWarn\b|\bNoWarn\s*=|<WarningsNotAsErrors\b|<TreatWarningsAsErrors>\s*false|<(RunAnalyzers|RunAnalyzersDuringBuild|EnableNETAnalyzers)>\s*false", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MsBuildSuppression();

    [GeneratedRegex(@"^\s*dotnet_(analyzer_)?diagnostic\.[^=\r\n]*severity\s*=\s*(none|silent|suggestion)\b", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ConfigSuppression();
}
