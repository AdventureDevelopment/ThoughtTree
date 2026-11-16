using System.Text.RegularExpressions;

namespace ThoughtTree.Infrastructure.Rules;

/// <summary>
/// ADR-046 rules for the GitHub workflows: Azure sign-in only through OpenID Connect, no stored
/// deployment secrets, production only through the approved environment, actions pinned to commits.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-046")]
public partial class WorkflowRules
{
    private static readonly IReadOnlyList<(string Name, string Text)> Workflows =
        Directory.GetFiles(Repository.PathOf(".github", "workflows"), "*.y*ml")
            .Order(StringComparer.Ordinal)
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();

    [Fact]
    public void No_stored_secret_is_used()
    {
        foreach (var (name, text) in Workflows)
        {
            SecretUse().Matches(text).Select(m => m.Groups[1].Value).Where(s => s != "GITHUB_TOKEN")
                .ShouldBeEmpty($"{name}: no stored deployment secrets (ADR-046); sign in with OpenID Connect");
        }
    }

    [Fact]
    public void Azure_sign_in_uses_openid_connect_only()
    {
        foreach (var (name, text) in Workflows.Where(w => w.Text.Contains("azure/login@", StringComparison.Ordinal)))
        {
            text.ShouldContain("id-token: write", customMessage: $"{name}: OpenID Connect needs id-token: write");
            text.ShouldContain("client-id: ${{ vars.", customMessage: $"{name}: identity ids come from repository variables");
            text.ShouldNotContain("creds:", customMessage: $"{name}: no service principal credentials");
            text.ShouldNotContain("client-secret", Case.Insensitive, $"{name}: no client secret");
        }
    }

    [Fact]
    public void Production_is_deployed_only_through_the_approved_environment()
    {
        foreach (var (name, text) in Workflows.Where(w => w.Text.Contains("AZURE_CLIENT_ID_PRODUCTION", StringComparison.Ordinal)))
        {
            text.ShouldContain("environment: production", customMessage: $"{name}: production needs the manual approval of the production environment (ADR-046)");
            text.ShouldNotContain("pull_request", customMessage: $"{name}: pull requests never reach production");
        }
    }

    [Fact]
    public void No_workflow_runs_untrusted_code_with_target_privileges()
    {
        foreach (var (name, text) in Workflows)
        {
            PullRequestTargetTrigger().IsMatch(text).ShouldBeFalse(name);
        }
    }

    [Fact]
    public void Every_action_is_pinned_to_a_full_commit()
    {
        foreach (var (name, text) in Workflows)
        {
            var unpinned = UsesLine().Matches(text).Select(m => m.Groups[1].Value)
                .Where(u => !u.StartsWith("./", StringComparison.Ordinal) && !PinnedRef().IsMatch(u));
            unpinned.ShouldBeEmpty($"{name}: pin actions to a full commit SHA");
        }
    }

    // As an "on:" key or list entry; comments that name it are fine.
    [GeneratedRegex(@"^[^#\n]*(^|[\s\[,])pull_request_target\s*(:|,|\]|$)", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex PullRequestTargetTrigger();

    [GeneratedRegex(@"\$\{\{\s*secrets\.([A-Za-z0-9_]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SecretUse();

    [GeneratedRegex(@"^\s*(?:-\s*)?uses:\s*([^\s#]+)", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex UsesLine();

    [GeneratedRegex(@"@[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex PinnedRef();
}
