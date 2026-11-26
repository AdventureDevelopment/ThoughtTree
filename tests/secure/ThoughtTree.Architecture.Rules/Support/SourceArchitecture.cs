using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;

namespace ThoughtTree.Architecture.Rules.Support;

/// <summary>The compiled source assemblies, loaded once for all type-level rules (ArchUnitNET).</summary>
internal static class SourceArchitecture
{
    public static ArchUnitNET.Domain.Architecture Instance { get; } = new ArchLoader()
        .LoadAssemblies(SourceProjects.All.Select(Load).ToArray())
        .Build();

    public static System.Reflection.Assembly Load(string project) => System.Reflection.Assembly.Load(project);

    /// <summary>
    /// Fails with every violation listed. Rules state WithoutRequiringPositiveResults, because the projects
    /// have no types yet (foundation stage 1); the rule All_six_source_assemblies_are_loaded checks separately
    /// that every assembly was loaded, so an empty match cannot come from a missing assembly.
    /// </summary>
    public static void ShouldHold(this IArchRule rule)
    {
        var results = rule.Evaluate(Instance).ToList();
        var violations = results.Where(result => !result.Passed).Select(result => result.Description).ToList();

        violations.ShouldBeEmpty(rule.Description);
    }
}
