using System.Text.RegularExpressions;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Harness;

/// <summary>ADR-054: tests are placed by intent, projects are named by kind, every test carries its Intent trait.</summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-054")]
public sealed partial class TestPlacementRules
{
    private static readonly Dictionary<string, string> IntentByFolder = new()
    {
        ["specify"] = "Specify",
        ["secure"] = "Secure",
        ["confirm"] = "Confirm",
    };

    private static readonly HashSet<string> Kinds =
        ["Specs", "Properties", "Model", "Compatibility", "Simulation", "Rules", "Contract", "Security", "Budgets", "EndToEnd", "Smoke"];

    [Fact]
    public void Test_projects_live_in_an_intent_folder_and_are_named_by_kind()
    {
        foreach (var project in ProjectFile.All("tests"))
        {
            var parts = Repository.Relative(project.Path).Split('/');
            parts.Length.ShouldBe(4, $"{project.Path}: expected tests/<intent>/<Project>/<Project>.csproj");
            parts[2].ShouldBe(project.Name);
            parts[1].ShouldBeOneOf("specify", "secure", "confirm", "support");
            project.Name.ShouldStartWith("ThoughtTree.");

            if (parts[1] != "support")
            {
                Kinds.ShouldContain(project.Name.Split('.')[^1], $"{project.Name}: name test projects by kind (ADR-054)");
            }
        }
    }

    [Fact]
    public void Every_test_class_carries_the_intent_of_its_folder()
    {
        foreach (var (folder, intent) in IntentByFolder)
        {
            foreach (var file in Repository.Files(Path.Combine("tests", folder), "*.cs"))
            {
                var text = File.ReadAllText(file);
                if (TestAttribute().IsMatch(text))
                {
                    text.ShouldContain($"[Trait(\"Intent\", \"{intent}\")]", customMessage: $"{Repository.Relative(file)} must carry [Trait(\"Intent\", \"{intent}\")]");
                }
            }
        }
    }

    [Fact]
    public void Support_projects_hold_no_tests()
    {
        Repository.Files(Path.Combine("tests", "support"), "*.cs")
            .Where(f => TestAttribute().IsMatch(File.ReadAllText(f)))
            .Select(Repository.Relative)
            .ShouldBeEmpty("tests/support holds shared helpers only (ADR-054)");
    }

    [Fact]
    public void Adr_traits_name_existing_adrs()
    {
        var existing = Directory.GetFiles(Repository.PathOf("docs", "adr"), "ADR-*.md")
            .Select(f => Path.GetFileName(f)[..7])
            .ToHashSet();

        var unknown = Repository.Files("tests", "*.cs")
            .SelectMany(f => AdrTrait().Matches(File.ReadAllText(f)).Select(m => (File: Repository.Relative(f), Id: m.Groups[1].Value)))
            .Where(t => !existing.Contains(t.Id))
            .Select(t => $"{t.File}: {t.Id}");

        unknown.ShouldBeEmpty();
    }

    [GeneratedRegex(@"\[(Fact|Theory|Scenario)\b", RegexOptions.CultureInvariant)]
    private static partial Regex TestAttribute();

    [GeneratedRegex(@"\[Trait\(""ADR"",\s*""(ADR-\d{3})""\)\]", RegexOptions.CultureInvariant)]
    internal static partial Regex AdrTrait();
}
