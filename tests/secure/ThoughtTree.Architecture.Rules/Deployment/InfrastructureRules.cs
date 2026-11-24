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
    private static IReadOnlyList<string> Workflows => Workflow.Files();

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

        foreach (var workflow in Workflow.All())
        {
            Workflow.GrantsWrite(workflow.Permissions, "id-token").ShouldBeFalse($"{workflow.Name}: grant id-token: write per job, not to the whole workflow");
            foreach (var job in workflow.Jobs.Where(j => j.Steps.Any(s => s.Run?.Contains("azure-sign-in.sh", StringComparison.Ordinal) == true)))
            {
                job.CanRequestIdToken.ShouldBeTrue($"{job}: the signing-in job needs id-token: write");
                job.Environment.ShouldNotBeNull($"{job}: signs in only inside a GitHub environment, whose branch rules and reviewers guard the identity");
            }
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

        foreach (var workflow in Workflow.All())
        {
            workflow.Permissions.ShouldNotBeNull($"{workflow.Name}: declare top-level permissions");
            workflow.Triggers.ShouldNotContain("pull_request_target", $"{workflow.Name}: pull_request_target would hand secrets and tokens to pull request code");
            Workflow.Scalar(workflow.Permissions).ShouldNotBe("write-all", $"{workflow.Name}: no write-all");
            workflow.Jobs.Where(j => Workflow.Scalar(Workflow.Child(j.Node, "permissions")) == "write-all").Select(j => j.ToString())
                .ShouldBeEmpty("no job gets write-all");
        }
    }

    [Fact]
    public void No_expression_is_expanded_inside_a_shell_script()
    {
        // ${{ }} in run: is pasted into the script before the shell sees it, so text from a branch name, title,
        // input or event becomes code. Values go through env: and are read as shell variables.
        var offenders = Workflow.All()
            .SelectMany(w => w.Jobs)
            .SelectMany(j => j.Steps.Where(s => s.Run?.Contains("${{", StringComparison.Ordinal) == true).Select(_ => j.ToString()))
            .ToList();

        offenders.ShouldBeEmpty("pass expressions through env:, never ${{ }} inside run:");
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

        var parsed = Workflow.Load(Repository.PathOf(".github", "workflows", "deploy-production.yml"));
        parsed.Triggers.SetEquals(["workflow_dispatch"]).ShouldBeTrue("production starts only by hand: workflow_dispatch and nothing else");
        parsed.Jobs.Where(j => j.CanRequestIdToken).ShouldAllBe(j => j.Environment == "production", "the job that signs in waits for the approval in the environment 'production'");
        Workflow.All().Where(w => w.Name != ".github/workflows/deploy-production.yml").SelectMany(w => w.Jobs)
            .Where(j => j.Environment == "production").Select(j => j.ToString())
            .ShouldBeEmpty("only deploy-production.yml uses the environment 'production'");

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
        var preview = File.ReadAllText(Repository.PathOf("infra", "preview.bicepparam"));

        test.ShouldContain("using 'main.bicep'");
        production.ShouldContain("using 'main.bicep'");
        preview.ShouldContain("using 'main.bicep'");
        ParameterNames(production).ShouldBe(ParameterNames(test), "ADR-046: both environments set the same parameters of the same template");
        ParameterNames(preview).ShouldBe(ParameterNames(test), "ADR-046: previews use the same template and parameters");
        Repository.Files("infra", "*.bicepparam").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)
            .ShouldBe(["preview.bicepparam", "production.bicepparam", "test.bicepparam"]);

        var main = File.ReadAllText(Repository.PathOf("infra", "main.bicep"));
        main.ShouldNotContain("environmentName ==", customMessage: "behaviour differs through parameters, not through the environment's name");
        EnvironmentNameUses(main).ShouldBeEmpty("environmentName only names and tags the resource; behaviour differs through other parameters");
    }

    [Fact]
    public void Every_site_stays_on_the_free_plan()
    {
        // Re-review N-L4: staticSites/write would allow another plan; every deployment re-applies main.bicep, which
        // fixes the plan to Free in the template itself, not through a parameter.
        var main = File.ReadAllText(Repository.PathOf("infra", "main.bicep"));
        FreeSku().IsMatch(main).ShouldBeTrue("main.bicep sets sku name and tier to the literal 'Free'");
        SkuLine().Count(main).ShouldBe(1, "one sku block");
        Repository.Files("infra", "*.bicepparam").Where(f => File.ReadAllText(f).Contains("sku", StringComparison.OrdinalIgnoreCase)).Select(Repository.Relative)
            .ShouldBeEmpty("the plan is not a parameter");
    }

    [Theory]
    [InlineData("  sku: {\n    name: skuName\n    tier: skuName\n  }\n", false)]
    [InlineData("  sku: {\n    name: 'Standard'\n    tier: 'Standard'\n  }\n", false)]
    [InlineData("  sku: {\n    name: 'Free'\n    tier: 'Free'\n  }\n", true)]
    public void The_free_plan_is_recognised(string bicep, bool free)
    {
        FreeSku().IsMatch(bicep).ShouldBe(free);
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
    [InlineData("token: ${{ " + "secrets['AZURE_TOKEN'] }}")]
    [InlineData("all: ${{ toJSON(" + "secrets) }}")]
    [InlineData("token: ${{ " + "secrets . AZURE_TOKEN }}")]
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
    [InlineData("on: pull_request" + "_target\n")]
    [InlineData("on: [push, pull_request" + "_target]\n")]
    [InlineData("on:\n  pull_request" + "_target:\n    types: [opened]\n")]
    [InlineData("\"on\": {pull_request" + "_target: {}}\n")]
    public void A_pull_request_target_trigger_is_parsed_in_every_style(string yaml)
    {
        Workflow.Parse(yaml + "jobs: {}\n").Triggers.ShouldContain("pull_request" + "_target");
    }

    [Theory]
    [InlineData("on: push\n", "push")]
    [InlineData("on: [push, workflow_dispatch]\n", "push")]
    [InlineData("on:\n  workflow_run:\n    workflows: [x]\n", "workflow_run")]
    [InlineData("on: {repository_dispatch: {}}\n", "repository_dispatch")]
    [InlineData("# workflow_dispatch only\non: [deployment]\n", "deployment")]
    public void An_automatic_trigger_is_parsed_in_every_style(string yaml, string trigger)
    {
        var triggers = Workflow.Parse(yaml + "jobs: {}\n").Triggers;
        triggers.ShouldContain(trigger);
        triggers.SetEquals(["workflow_dispatch"]).ShouldBeFalse();
    }

    [Theory]
    [InlineData("param environmentName string\nvar x = environmentName == 'production' ? 1 : 2\n", 1)]
    [InlineData("var x = 'production' == environmentName\n", 1)]
    [InlineData("var x = environmentName=='production'\n", 1)]
    [InlineData("var x = contains(environmentName, 'prod')\n", 1)]
    [InlineData("@allowed([\n  'test'\n])\nparam environmentName string\nresource s 'x' = {\n  name: 'stapp-thoughttree-${environmentName}'\n  tags: {\n    environment: environmentName\n  }\n}\n", 0)]
    public void Uses_of_the_environment_name_are_found(string bicep, int count)
    {
        EnvironmentNameUses(bicep).Count().ShouldBe(count);
    }

    [Theory]
    [InlineData("az resource create --id x")]
    [InlineData("az rest --method put --url x")]
    [InlineData("az deployment group create -g x --template-file other.bicep")]
    [InlineData("az deployment sub create --location x")]
    [InlineData("az staticwebapp create -n x")]
    [InlineData("az staticwebapp appsettings set -n x")]
    public void An_imperative_resource_change_is_found(string command)
    {
        ImperativeResource().IsMatch(command).ShouldBeTrue();
    }

    [Theory]
    [InlineData("az rest --method put --url \"$site_id/builds/$preview?api-version=2024-04-01\" --output none")]
    [InlineData("az rest --method delete --url \"$other?api-version=2024-04-01\" --output none")]
    [InlineData("az rest --method delete --url \"$site_id?api-version=2024-04-01\" --output none")]
    [InlineData("az rest --url \"$site_id/builds/$preview?api-version=2024-04-01\" --method delete")]
    public void Every_other_rest_call_stays_forbidden(string command)
    {
        ImperativeResource().IsMatch(command).ShouldBeTrue();
    }

    [Fact]
    public void Deleting_a_preview_environment_by_its_resource_is_not_imperative()
    {
        // The one allowed REST call (preview.sh prune): DELETE of a staging environment of the preview site, which
        // the site deployer role allows (staticSites/builds/delete) and which returns without polling.
        ImperativeResource().IsMatch("az rest --method delete --url \"$site_id/builds/$preview?api-version=2024-04-01\" --output none").ShouldBeFalse();
    }

    [Fact]
    public void No_deploy_script_waits_on_an_operation_status_outside_its_resource_group()
    {
        // These commands poll Microsoft.Web/locations/staticSitesOperationStatuses at subscription scope, which the
        // site deployer role (resource-group scope) cannot read: preview-deploy run 45420685246 failed that way.
        var offenders = Repository.Files(Path.Combine("eng", "deploy"), "*.sh")
            .SelectMany(f => PollingDelete().Matches(string.Join('\n', File.ReadAllLines(f).Where(l => !l.TrimStart().StartsWith('#'))))
                .Select(m => $"{Repository.Relative(f)}: {m.Value}"))
            .ToList();

        offenders.ShouldBeEmpty("delete staging environments with az rest --method delete and wait on the environment list (eng/deploy/preview.sh)");
    }

    [Fact]
    public void Applying_the_main_template_is_not_imperative()
    {
        ImperativeResource().IsMatch("az deployment group create \\\n  --template-file infra/main.bicep").ShouldBeFalse();
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

    /// <summary>Lines of a template that use environmentName for anything but its declaration, the resource name
    /// and the environment tag.</summary>
    internal static IEnumerable<string> EnvironmentNameUses(string bicep) =>
        bicep.Split('\n')
            .Where(l => l.Contains("environmentName", StringComparison.Ordinal))
            .Where(l => !AllowedEnvironmentNameUse().IsMatch(l))
            .Select(l => l.Trim());

    private static List<string> ParameterNames(string text) =>
        ParameterLine().Matches(text).Select(m => m.Groups[1].Value).Order(StringComparer.Ordinal).ToList();

    [GeneratedRegex(@"secrets[ \t]*\.[ \t]*[A-Za-z_][A-Za-z0-9_]*|secrets[ \t]*\[|toJSON\([ \t]*secrets[ \t]*\)|secrets:\s*inherit", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
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

    [GeneratedRegex(@"sku:\s*\{\s*name:\s*'Free'\s*tier:\s*'Free'\s*\}", RegexOptions.CultureInvariant)]
    private static partial Regex FreeSku();

    [GeneratedRegex(@"\bsku\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex SkuLine();

    [GeneratedRegex(@"^param\s+(\w+)\s*=", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex ParameterLine();

    [GeneratedRegex(@"Microsoft\.Insights|Microsoft\.OperationalInsights|Microsoft\.Monitor|diagnosticSettings|applicationInsights|logAnalytics", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TelemetryResource();

    [GeneratedRegex(@"^\s*(param\s+environmentName\s+string\s*|name:\s*'[a-z-]+\$\{environmentName\}'\s*|environment:\s*environmentName\s*)$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedEnvironmentNameUse();

    [GeneratedRegex(@"az\s+(group|staticwebapp|identity|role\s+assignment|role\s+definition|resource)\s+create\b|az\s+rest\b(?!\s+--method\s+delete\s+--url\s+""\$site_id/builds/\$preview\?api-version=[0-9-]+""\s)|az\s+resource\s+(update|patch|tag)\b|az\s+deployment\s+(sub|mg|tenant)\b|--template-file\s+(?!infra/main\.bicep\b)\S+|--template-uri\b|az\s+staticwebapp\s+(update|appsettings|hostname|users|identity|backends|functions|enterprise-edge)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ImperativeResource();

    [GeneratedRegex(@"az\s+staticwebapp\s+(environment\s+)?delete\b|az\s+resource\s+delete\b", RegexOptions.CultureInvariant)]
    private static partial Regex PollingDelete();
}
