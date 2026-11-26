using ThoughtTree.Architecture.Rules.Support;
using static ThoughtTree.Architecture.Rules.Support.SourceProjects;

namespace ThoughtTree.Architecture.Rules.Dependencies;

/// <summary>
/// Project-level rules. An unused project reference leaves no trace in the compiled assembly,
/// so these rules read the project files; the type-level rules are in <see cref="TypeDependencyRules"/>.
/// </summary>
[Trait("Intent", "Secure")]
public sealed class ProjectReferenceRules
{
    private static readonly IReadOnlyList<ProjectFile> Sources = ProjectFile.All("src");

    private static ProjectFile Source(string name) => Sources.Single(project => project.Name == name);

    [Fact]
    [Trait("ADR", "ADR-054")]
    public void Source_folder_holds_exactly_the_six_projects()
    {
        Sources.Select(project => project.Name).Order(StringComparer.Ordinal)
            .ShouldBe(All.Order(StringComparer.Ordinal), "ADR-054: a further project needs a real boundary and an ADR change");

        foreach (var project in Sources)
        {
            Repository.Relative(project.Path).ShouldBe($"src/{project.Name}/{project.Name}.csproj");
        }
    }

    [Fact]
    [Trait("ADR", "ADR-054")]
    public void No_project_is_a_shared_or_common_project()
    {
        var offenders = ProjectFile.All("src").Concat(ProjectFile.All("tests"))
            .Select(project => project.Name)
            .Where(name => name.Contains("Shared", StringComparison.OrdinalIgnoreCase) || name.Contains("Common", StringComparison.OrdinalIgnoreCase));

        offenders.ShouldBeEmpty("ADR-054: no Shared or Common projects");
    }

    [Theory]
    [Trait("ADR", "ADR-027")]
    [Trait("ADR", "ADR-053")]
    [MemberData(nameof(SourceNames))]
    public void Project_references_point_inwards_only(string project)
    {
        var forbidden = Source(project).ProjectReferences.Except(AllowedReferences[project]).ToList();

        forbidden.ShouldBeEmpty(
            $"ADR-027/ADR-053: {project} may reference only [{string.Join(", ", AllowedReferences[project])}]");
    }

    [Theory]
    [Trait("ADR", "ADR-027")]
    [InlineData(ServerProject)]
    [InlineData(ServerCoreProject)]
    public void Server_projects_do_not_reference_domain(string project)
    {
        Source(project).ProjectReferences.ShouldNotContain(DomainProject, "ADR-027: the server cannot decrypt events and never sees Domain");
    }

    [Fact]
    [Trait("ADR", "ADR-053")]
    [Trait("ADR", "ADR-027")]
    public void Domain_depends_on_nothing()
    {
        var domain = Source(DomainProject);

        domain.Sdk.ShouldBe("Microsoft.NET.Sdk");
        domain.ProjectReferences.ShouldBeEmpty("ADR-053: Domain depends on nothing");
        domain.PackageReferences.ShouldBeEmpty("ADR-053: Domain depends on nothing");
        domain.FrameworkReferences.ShouldBeEmpty("ADR-027: no browser, storage or network in Domain");
    }

    [Fact]
    [Trait("ADR", "ADR-040")]
    public void Server_core_does_not_reference_aspnet_core()
    {
        var core = Source(ServerCoreProject);

        core.Sdk.ShouldBe("Microsoft.NET.Sdk", "ADR-040: Server.Core is a plain library");
        core.FrameworkReferences.ShouldNotContain(reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        core.PackageReferences.ShouldNotContain(reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("ADR", "ADR-041")]
    public void Client_application_layer_is_plain_csharp()
    {
        var app = Source(ClientAppProject);

        app.Sdk.ShouldBe("Microsoft.NET.Sdk", "ADR-041: Client.App has no Razor and no browser dependencies");
        app.FrameworkReferences.ShouldBeEmpty();
        app.PackageReferences.ShouldNotContain(reference =>
            reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) || reference.StartsWith("Microsoft.JSInterop", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("ADR", "ADR-053")]
    public void Transitive_project_references_are_off_so_client_cannot_reach_domain_through_client_app()
    {
        File.ReadAllText(Repository.PathOf("src", "Directory.Build.props"))
            .ShouldContain("<DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>");
    }

    public static TheoryData<string> SourceNames() => [.. All];
}
