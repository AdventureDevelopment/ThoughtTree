using System.Text.RegularExpressions;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Deployment;

/// <summary>
/// ADR-046: environments only from the Bicep templates in infra/, sign-in to Azure only through OpenID Connect,
/// no stored deployment secrets, production only after a manual approval, test and production identical apart
/// from parameters, no telemetry service. Static checks of infra/, .github/workflows and eng/deploy.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-046")]
public partial class InfrastructureRules
{
    private static IReadOnlyList<string> Workflows => Repository.Files(Path.Combine(".github", "workflows"), "*.yml");

    private static IReadOnlyList<string> DeployScripts => Repository.Files(Path.Combine("eng", "deploy"), "*.sh");

    [Fact]
    public void Workflows_read_no_stored_secret()
    {
        var offenders = Workflows.SelectMany(f => StoredSecrets(File.ReadAllText(f)).Select(s => $"{Repository.Relative(f)}: {s}")).ToList();

        offenders.ShouldBeEmpty("ADR-046: no deployment secrets are stored; only the job's own GITHUB_TOKEN may be read");
    }

    [Fact]
    public void Azure_sign_in_uses_only_openid_connect()
    {
        var files = Workflows.Concat(DeployScripts).ToList();
        var offenders = files
            .SelectMany(f => PasswordSignIn().Matches(File.ReadAllText(f)).Select(m => $"{Repository.Relative(f)}: {m.Value}"))
            .ToList();
        offenders.ShouldBeEmpty("ADR-046: sign in through OpenID Connect federation, never with a password, client secret or stored token");

        foreach (var workflow in Workflows.Where(f => File.ReadAllText(f).Contains("azure-sign-in.sh", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(workflow);
            JobLevelIdToken().IsMatch(text).ShouldBeTrue($"{Repository.Relative(workflow)}: the signing-in job needs id-token: write");
            TopLevelIdToken().IsMatch(text).ShouldBeFalse($"{Repository.Relative(workflow)}: grant id-token: write per job, not to the whole workflow");
        }
    }

    [Fact]
    public void Every_workflow_declares_its_permissions_and_never_runs_on_pull_request_target()
    {
        foreach (var workflow in Workflows)
        {
            var text = File.ReadAllText(workflow);
            TopLevelPermissions().IsMatch(text).ShouldBeTrue($"{Repository.Relative(workflow)}: declare top-level permissions");
            PullRequestTargetTrigger().IsMatch(text).ShouldBeFalse($"{Repository.Relative(workflow)}: pull_request_target would hand secrets and tokens to pull request code");
        }
    }

    [Fact]
    public void Actions_are_pinned_to_a_commit()
    {
        var offenders = Workflows
            .SelectMany(f => UnpinnedActions(File.ReadAllText(f)).Select(a => $"{Repository.Relative(f)}: {a}"))
            .ToList();

        offenders.ShouldBeEmpty("ADR-046: pin every action to a full commit id");
    }

    [Fact]
    public void Production_is_deployed_only_by_hand_after_approval()
    {
        var production = File.ReadAllText(Repository.PathOf(".github", "workflows", "deploy-production.yml"));
        production.ShouldContain("workflow_dispatch:");
        AutomaticTrigger().IsMatch(production).ShouldBeFalse("production starts only by hand");
        production.ShouldContain("environment: production", customMessage: "the GitHub environment 'production' holds the owner's approval");

        var others = Workflows.Where(f => !f.EndsWith("deploy-production.yml", StringComparison.Ordinal));
        foreach (var workflow in others)
        {
            var text = File.ReadAllText(workflow);
            (text.Contains("AZURE_CLIENT_ID_PRODUCTION", StringComparison.Ordinal) || text.Contains("rg-thoughttree-prod", StringComparison.Ordinal))
                .ShouldBeFalse($"{Repository.Relative(workflow)}: only deploy-production.yml may reach production");
        }
    }

    [Fact]
    public void Test_and_production_differ_only_in_parameter_values()
    {
        var test = File.ReadAllText(Repository.PathOf("infra", "test.bicepparam"));
        var production = File.ReadAllText(Repository.PathOf("infra", "production.bicepparam"));

        test.ShouldContain("using 'main.bicep'");
        production.ShouldContain("using 'main.bicep'");
        ParameterNames(production).ShouldBe(ParameterNames(test), "ADR-046: both environments set the same parameters of the same template");
        File.ReadAllText(Repository.PathOf("infra", "main.bicep")).ShouldNotContain("environmentName ==", customMessage: "behaviour differs through parameters, not through the environment's name");
    }

    [Fact]
    public void No_telemetry_or_logging_service_is_deployed()
    {
        var offenders = Repository.Files("infra", "*.bicep")
            .Where(f => TelemetryResource().IsMatch(File.ReadAllText(f)))
            .Select(Repository.Relative)
            .ToList();

        offenders.ShouldBeEmpty("ADR-046: no telemetry service such as Application Insights, no diagnostic logs");
    }

    [Fact]
    public void Deploy_scripts_create_no_infrastructure_outside_the_templates()
    {
        var offenders = DeployScripts
            .SelectMany(f => ImperativeResource().Matches(File.ReadAllText(f)).Select(m => $"{Repository.Relative(f)}: {m.Value}"))
            .ToList();

        offenders.ShouldBeEmpty("ADR-046: resources come only from the Bicep templates in infra/");
    }

    // Samples assembled from pieces, so that this file does not look like a workflow that breaks the rules.
    [Theory]
    [InlineData("token: ${{ " + "secrets.AZURE_TOKEN }}")]
    [InlineData("  " + "secrets: inherit")]
    public void A_stored_secret_is_found(string text)
    {
        StoredSecrets(text).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("on:\n  pull_request" + "_target:\n", true)]
    [InlineData("on: [push, pull_request" + "_target]\n", true)]
    [InlineData("# Runs on pull_request only (never pull_request" + "_target)\n", false)]
    public void A_pull_request_target_trigger_is_found(string text, bool found)
    {
        PullRequestTargetTrigger().IsMatch(text).ShouldBe(found);
    }

    [Fact]
    public void The_job_token_is_not_a_stored_secret()
    {
        StoredSecrets("repo_token: ${{ " + "secrets.GITHUB_TOKEN }}").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("- uses: actions/checkout@v4", true)]
    [InlineData("- uses: azure/login@main", true)]
    [InlineData("uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1", false)]
    [InlineData("uses: ./.github/workflows/checks.yml", false)]
    public void Unpinned_actions_are_found(string line, bool unpinned)
    {
        UnpinnedActions(line).Any().ShouldBe(unpinned);
    }

    [Theory]
    [InlineData("az login --service-principal -u x " + "--password y")]
    [InlineData("client-" + "secret: x")]
    [InlineData("creds: ${{ x }}")]
    [InlineData("azure_static_web_apps_" + "api_token: x")]
    public void A_password_sign_in_is_found(string text)
    {
        PasswordSignIn().IsMatch(text).ShouldBeTrue();
    }

    internal static IEnumerable<string> StoredSecrets(string text) =>
        SecretReference().Matches(text).Select(m => m.Value).Where(v => !v.EndsWith(".GITHUB_TOKEN", StringComparison.Ordinal));

    internal static IEnumerable<string> UnpinnedActions(string text) =>
        UsesLine().Matches(text).Select(m => m.Groups[1].Value)
            .Where(a => !a.StartsWith("./", StringComparison.Ordinal) && !PinnedRef().IsMatch(a));

    private static List<string> ParameterNames(string text) =>
        ParameterLine().Matches(text).Select(m => m.Groups[1].Value).Order(StringComparer.Ordinal).ToList();

    [GeneratedRegex(@"secrets\.[A-Za-z0-9_]+|secrets:\s*inherit", RegexOptions.CultureInvariant)]
    private static partial Regex SecretReference();

    [GeneratedRegex(@"--password\b|client-secret|^\s*creds:|azure_static_web_apps_api_token", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PasswordSignIn();

    [GeneratedRegex(@"^[ ]{4,}id-token:[ ]*write", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex JobLevelIdToken();

    [GeneratedRegex(@"^[ ]{0,2}id-token:[ ]*write", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex TopLevelIdToken();

    [GeneratedRegex(@"^[ ]*-?[ ]*pull_request_target\b|^on:[ ]*\[?[^#\n]*pull_request_target", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex PullRequestTargetTrigger();

    [GeneratedRegex(@"^permissions:", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex TopLevelPermissions();

    [GeneratedRegex(@"uses:\s*([^\s#]+)", RegexOptions.CultureInvariant)]
    private static partial Regex UsesLine();

    [GeneratedRegex(@"@[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex PinnedRef();

    [GeneratedRegex(@"^\s{2}(push|pull_request|schedule|workflow_run|release):", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex AutomaticTrigger();

    [GeneratedRegex(@"^param\s+(\w+)\s*=", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex ParameterLine();

    [GeneratedRegex(@"Microsoft\.Insights|Microsoft\.OperationalInsights|diagnosticSettings|applicationInsights", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TelemetryResource();

    [GeneratedRegex(@"az\s+(group|staticwebapp|identity|role\s+assignment|role\s+definition)\s+create\b", RegexOptions.CultureInvariant)]
    private static partial Regex ImperativeResource();
}
