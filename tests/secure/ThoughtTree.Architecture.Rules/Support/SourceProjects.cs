namespace ThoughtTree.Architecture.Rules.Support;

/// <summary>The six source projects of ADR-054 and the namespaces that separate layers inside them.</summary>
internal static class SourceProjects
{
    public const string DomainProject = "ThoughtTree.Domain";
    public const string ContractsProject = "ThoughtTree.Contracts";
    public const string ClientAppProject = "ThoughtTree.Client.App";
    public const string ClientProject = "ThoughtTree.Client";
    public const string ServerCoreProject = "ThoughtTree.Server.Core";
    public const string ServerProject = "ThoughtTree.Server";

    /// <summary>Browser adapters inside the Client project; the only place for JavaScript interop (ADR-042).</summary>
    public const string ClientAdapters = "ThoughtTree.Client.Adapters";

    /// <summary>ASP.NET Core endpoints and hub inside the Server project; the hosting shell of ADR-039.</summary>
    public const string ServerEndpoints = "ThoughtTree.Server.Endpoints";

    /// <summary>Store adapters inside the Server project (ADR-053).</summary>
    public const string ServerStores = "ThoughtTree.Server.Stores";

    public static readonly IReadOnlyList<string> All = [DomainProject, ContractsProject, ClientAppProject, ClientProject, ServerCoreProject, ServerProject];

    /// <summary>Allowed project references: dependencies point inwards only (ADR-027, ADR-053).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedReferences =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [DomainProject] = new HashSet<string>(),
            [ContractsProject] = new HashSet<string>(),
            [ClientAppProject] = new HashSet<string> { DomainProject, ContractsProject },
            [ClientProject] = new HashSet<string> { ClientAppProject, ContractsProject },
            [ServerCoreProject] = new HashSet<string> { ContractsProject },
            [ServerProject] = new HashSet<string> { ServerCoreProject, ContractsProject },
        };
}
