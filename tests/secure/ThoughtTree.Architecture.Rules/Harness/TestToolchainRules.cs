using System.Xml.Linq;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Harness;

/// <summary>ADR-075: xUnit v2 and LambdaTale 2.9.x, versions only in Directory.Packages.props, no other spec library.</summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-075")]
public sealed class TestToolchainRules
{
    private static readonly string[] OtherSpecLibraries =
        ["SpecFlow", "Reqnroll", "LightBDD", "Xbehave", "TestStack.BDDfy", "Gherkin", "NSpec", "Machine.Specifications"];

    private static readonly Dictionary<string, string> CentralVersions = XDocument.Load(Repository.PathOf("Directory.Packages.props"))
        .Descendants("PackageVersion")
        .ToDictionary(packageVersion => (string)packageVersion.Attribute("Include")!, packageVersion => (string)packageVersion.Attribute("Version")!);

    [Theory]
    [InlineData("xunit")]
    [InlineData("bbv.LambdaTale")]
    public void Test_framework_and_spec_library_stay_on_2_9(string package)
    {
        CentralVersions[package].ShouldStartWith("2.9.");
    }

    [Fact]
    public void No_xunit_v3_and_no_other_spec_library()
    {
        var packages = CentralVersions.Keys.Concat(AllProjects().SelectMany(project => project.PackageReferences)).ToList();

        packages.ShouldNotContain(package => package.StartsWith("xunit.v3", StringComparison.OrdinalIgnoreCase));
        packages.ShouldNotContain(package => OtherSpecLibraries.Any(library => package.StartsWith(library, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Package_versions_live_only_in_directory_packages_props()
    {
        var withVersion = Repository.Files("src", "*.csproj").Concat(Repository.Files("tests", "*.csproj"))
            .Where(projectFile => XDocument.Load(projectFile).Descendants("PackageReference").Any(reference => reference.Attribute("Version") is not null || reference.Attribute("VersionOverride") is not null))
            .Select(Repository.Relative);

        withVersion.ShouldBeEmpty();
    }

    [Fact]
    public void Every_spec_project_uses_lambdatale()
    {
        foreach (var project in ProjectFile.All(Path.Combine("tests", "specify")))
        {
            project.PackageReferences.ShouldContain("bbv.LambdaTale", $"{project.Name}: specs are written with LambdaTale (ADR-054, ADR-075)");
        }
    }

    private static IEnumerable<ProjectFile> AllProjects() => ProjectFile.All("src").Concat(ProjectFile.All("tests"));
}
