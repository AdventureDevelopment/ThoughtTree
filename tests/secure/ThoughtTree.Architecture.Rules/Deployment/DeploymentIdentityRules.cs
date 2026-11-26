using System.Text.RegularExpressions;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Deployment;

/// <summary>
/// ADR-046 (production only after a manual approval; no credentials an agent could see or leak): the deployment
/// identities cannot change their own sign-in. Identities and federated credentials live in their own resource
/// group, created only by the owner-run bootstrap; the pipeline identities get only a narrow custom role on their
/// site's group, with no right to write role assignments, role definitions, identities or federated credentials.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-046")]
public sealed partial class DeploymentIdentityRules
{
    // Built-in roles that can write federated credentials or role assignments: Owner, Contributor,
    // User Access Administrator, Role Based Access Control Administrator, Managed Identity Contributor.
    private static readonly string[] PrivilegedBuiltInRoles =
    [
        "8e3af657-a8ff-443c-a75c-2fe8c4bcb635",
        "b24988ac-6180-42a0-ab88-20f7382dd24c",
        "18d7d88d-d35e-4fb5-a5c3-7773c20a72d9",
        "f58310d9-a9f6-439a-9e8d-f62e7b41a168",
        "e40ec5ca-96e0-45a2-b4ff-59039f2c2b59",
    ];

    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.Resources/subscriptions/resourceGroups/read",
        "Microsoft.Resources/deployments/*",
        "Microsoft.Web/staticSites/read",
        "Microsoft.Web/staticSites/write",
        "Microsoft.Web/staticSites/listSecrets/action",
        "Microsoft.Web/staticSites/builds/read",
        "Microsoft.Web/staticSites/builds/delete",
    };

    private static IReadOnlyList<string> Templates => Repository.Files("infra", "*.bicep");

    private static string Bootstrap => File.ReadAllText(Repository.PathOf("infra", "bootstrap", "bootstrap.bicep"));

    [Fact]
    public void No_template_assigns_a_built_in_role_that_can_change_sign_in_or_access()
    {
        var offenders = Templates
            .SelectMany(f => PrivilegedBuiltInRoles.Where(id => File.ReadAllText(f).Contains(id, StringComparison.OrdinalIgnoreCase)).Select(id => $"{Repository.Relative(f)}: {id}"))
            .ToList();

        offenders.ShouldBeEmpty("Owner, Contributor and the access administrator roles can add federated credentials or role assignments; use the custom role");
    }

    [Fact]
    public void Custom_roles_allow_only_the_actions_a_site_deployment_needs()
    {
        var actions = Templates.SelectMany(f => RoleActions(File.ReadAllText(f)).Select(a => (File: Repository.Relative(f), Action: a))).ToList();

        actions.ShouldNotBeEmpty("bootstrap.bicep defines the custom role 'ThoughtTree site deployer'");
        actions.Where(a => !AllowedActions.Contains(a.Action)).Select(a => $"{a.File}: {a.Action}")
            .ShouldBeEmpty("no Microsoft.Authorization, Microsoft.ManagedIdentity or wildcard actions for deployment identities");
    }

    [Fact]
    public void Identities_and_their_access_come_only_from_the_owner_run_bootstrap()
    {
        var applied = Repository.Files("infra", "*.bicep").Where(f => !Repository.Relative(f).StartsWith("infra/bootstrap/", StringComparison.Ordinal));
        foreach (var template in applied)
        {
            var text = File.ReadAllText(template);
            IdentityOrAccessResource().IsMatch(text).ShouldBeFalse($"{Repository.Relative(template)}: the pipeline applies this template; identities, federated credentials and role assignments belong in infra/bootstrap");
        }

        foreach (var script in Repository.Files(Path.Combine("eng", "deploy"), "*.sh"))
        {
            AzIdentityOrRoleCommand().IsMatch(File.ReadAllText(script)).ShouldBeFalse($"{Repository.Relative(script)}: no identity or role commands in the pipeline");
        }
    }

    [Fact]
    public void Identities_live_in_their_own_group_and_get_access_only_to_site_groups()
    {
        var modules = Modules(Bootstrap).ToList();

        var identityModules = modules.Where(m => m.File == "deploy-identity.bicep").ToList();
        identityModules.ShouldNotBeEmpty();
        identityModules.ShouldAllBe(m => m.Scope == "identityGroup", "identities and federated credentials live in rg-thoughttree-identity");

        var accessModules = modules.Where(m => m.File == "site-access.bicep").ToList();
        accessModules.ShouldNotBeEmpty();
        accessModules.ShouldAllBe(m => m.Scope.StartsWith("siteGroups", StringComparison.Ordinal), "role assignments only on the site groups, never on the identity group");

        Bootstrap.ShouldContain("name: 'rg-thoughttree-identity'");
        File.ReadAllText(Repository.PathOf("infra", "bootstrap", "deploy-identity.bicep")).ShouldNotContain("roleAssignments", customMessage: "the identity module grants no access itself");
    }

    [Theory]
    [InlineData("actions: [\n  'Microsoft.Web/staticSites/read'\n  'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials/write'\n]", "Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials/write")]
    [InlineData("actions: [ '*' ]", "*")]
    public void Role_actions_are_read_from_the_template(string text, string expected)
    {
        RoleActions(text).ShouldContain(expected);
    }

    private static IEnumerable<string> RoleActions(string text) =>
        ActionsBlock().Matches(text).SelectMany(m => QuotedString().Matches(m.Groups[1].Value).Select(q => q.Groups[1].Value));

    private static IEnumerable<(string File, string Scope)> Modules(string text) =>
        ModuleBlock().Matches(text).Select(m => (m.Groups[1].Value, m.Groups[2].Value.Trim()));

    [GeneratedRegex(@"\bactions:\s*\[(.*?)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ActionsBlock();

    [GeneratedRegex(@"'([^']*)'", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedString();

    [GeneratedRegex(@"^module\s+\w+\s+'([^']+)'\s*=.*?^\s*scope:\s*([^\r\n]+)", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex ModuleBlock();

    [GeneratedRegex(@"Microsoft\.ManagedIdentity|Microsoft\.Authorization", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IdentityOrAccessResource();

    [GeneratedRegex(@"az\s+(identity|role|ad)\b", RegexOptions.CultureInvariant)]
    private static partial Regex AzIdentityOrRoleCommand();
}
