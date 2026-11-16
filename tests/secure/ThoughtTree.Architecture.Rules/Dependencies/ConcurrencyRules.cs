using ThoughtTree.Architecture.Rules.Support;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static ThoughtTree.Architecture.Rules.Support.DependencyConditions;
using static ThoughtTree.Architecture.Rules.Support.SourceArchitecture;
using static ThoughtTree.Architecture.Rules.Support.SourceProjects;

namespace ThoughtTree.Architecture.Rules.Dependencies;

/// <summary>
/// ADR-039 per namespace. Domain, Client.App and Server.Core are also covered by the banned API analyzer
/// (src/BannedSymbols.Concurrency.txt); Client and Server share hosting and logic in one project,
/// so only their hosting namespaces may use threads, timers and delays without TimeProvider.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-039")]
public class ConcurrencyRules
{
    private const string ForbiddenTypes =
        @"^(System\.Threading\.(Thread|ThreadPool|Timer|Tasks\.Parallel|Tasks\.TaskFactory(`1)?)|System\.Timers\.Timer)$";

    // Task.Run (all overloads), Task.Factory, and Task.Delay / WaitAsync / CancellationTokenSource without a TimeProvider.
    private const string ForbiddenMethods =
        @"System\.Threading\.Tasks\.Task(`1)?::(Run(<.*>)?\(.*\)|get_Factory\(\)|Delay\((System\.Int32|System\.TimeSpan)(,System\.Threading\.CancellationToken)?\)|WaitAsync\(System\.TimeSpan(,System\.Threading\.CancellationToken)?\))$"
        + @"|System\.Threading\.CancellationTokenSource::\.ctor\((System\.Int32|System\.TimeSpan)\)$";

    private static ArchUnitNET.Fluent.Syntax.Elements.Types.GivenTypesConjunction TypesOutsideHosting() =>
        Types().That().ResideInAssembly(Load(DomainProject), Load(ContractsProject), Load(ClientAppProject), Load(ServerCoreProject))
            .Or().ResideInAssembly(Load(ClientProject)).And().DoNotResideInNamespaceOrBelow(ClientAdapters)
            .Or().ResideInAssembly(Load(ServerProject)).And().DoNotResideInNamespaceOrBelow(ServerEndpoints);

    [Fact]
    public void Only_hosting_namespaces_use_threads_and_timers()
    {
        TypesOutsideHosting()
            .Should().FollowCustomCondition(UsesNoTypeMatching(ForbiddenTypes), "use no threads, thread pool, Parallel or timers")
            .Because($"ADR-039: only {ClientAdapters} and {ServerEndpoints} may use threads and timers; use TimeProvider")
            .WithoutRequiringPositiveResults()
            .ShouldHold();
    }

    [Fact]
    public void Only_hosting_namespaces_run_tasks_or_wait_without_time_provider()
    {
        TypesOutsideHosting()
            .Should().FollowCustomCondition(CallsNoMethodMatching(ForbiddenMethods), "not call Task.Run or wait without TimeProvider")
            .Because("ADR-039: no Task.Run; wait only through TimeProvider (Task.Delay(TimeSpan, TimeProvider))")
            .WithoutRequiringPositiveResults()
            .ShouldHold();
    }
}
