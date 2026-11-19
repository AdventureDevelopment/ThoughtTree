using System.Text.RegularExpressions;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Deployment;

/// <summary>
/// ADR-046 ("no credentials an agent could see or leak"; architecture.md section 7: agents never see deployment
/// credentials): code from a pull request runs only in jobs that cannot obtain an OpenID Connect token, and the
/// workflow that deploys a preview runs from main, takes the build as data and never checks out pull request code.
/// Previews go to their own site, so nothing a pull request can reach writes to the test or production site.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-046")]
public partial class PullRequestIsolationRules
{
    // merge_group runs the queued pull request's code before it is merged (re-review N-M2).
    private static readonly string[] PullRequestEvents = ["pull_request", "pull_request_target", "pull_request_review", "pull_request_review_comment", "issue_comment", "merge_group"];

    private const string ForkFilter = "github.event.workflow_run.head_repository.full_name == github.repository";

    [Fact]
    public void No_job_that_runs_pull_request_code_can_request_an_openid_connect_token()
    {
        var offenders = Workflow.All()
            .Where(w => w.Triggers.Overlaps(PullRequestEvents))
            .SelectMany(w => w.Jobs)
            .SelectMany(OpenIdConnectJobs)
            .ToList();

        offenders.ShouldBeEmpty("a pull_request run executes the pull request's own code and workflow file; it must get no id-token: write (preview builds upload an artifact, a workflow_run from main deploys it)");
    }

    [Fact]
    public void No_job_that_runs_pull_request_code_uses_a_deployment_environment()
    {
        var offenders = Workflow.All()
            .Where(w => w.Triggers.Overlaps(PullRequestEvents))
            .SelectMany(w => w.Jobs)
            .Where(j => j.Environment is not null)
            .Select(j => $"{j}: environment {j.Environment}")
            .ToList();

        offenders.ShouldBeEmpty("deployment environments hold the sign-in identities; pull request code never runs in one");
    }

    [Fact]
    public void Workflow_run_workflows_never_check_out_the_code_that_triggered_them()
    {
        var offenders = Workflow.All()
            .Where(w => w.Triggers.Contains("workflow_run"))
            .SelectMany(w => w.Jobs)
            .SelectMany(j => j.Steps.Where(s => s.Uses?.StartsWith("actions/checkout@", StringComparison.Ordinal) == true)
                .Where(s => s.HasWith("ref") || s.HasWith("repository"))
                .Select(_ => $"{j}: checkout with ref or repository"))
            .ToList();

        offenders.ShouldBeEmpty("a workflow_run job checks out main only; the pull request's build arrives as an artifact, as data");
    }

    [Fact]
    public void Workflow_run_jobs_take_other_runs_only_as_data_through_site_artifact_py()
    {
        Workflow.All().Where(w => w.Triggers.Contains("workflow_run")).SelectMany(WorkflowRunViolations).ToList()
            .ShouldBeEmpty("a workflow_run job never fetches, checks out or downloads the triggering code; artifacts come only through eng/deploy/site_artifact.py fetch");
    }

    [Fact]
    public void Workflow_run_jobs_with_a_credential_skip_runs_from_forks()
    {
        Workflow.All().Where(w => w.Triggers.Contains("workflow_run")).SelectMany(ForkFilterViolations).ToList()
            .ShouldBeEmpty($"every job that can get an OpenID Connect token and runs on workflow_run has the condition '{ForkFilter}'");
    }

    [Fact]
    public void Every_job_with_an_openid_connect_token_runs_in_an_environment_and_calls_no_remote_workflow()
    {
        Workflow.All().SelectMany(CredentialJobViolations).ToList()
            .ShouldBeEmpty("environments hold the identities and their branch rules; reusable workflows only from this repository, followed to any depth");
    }

    [Fact]
    public void Only_deploy_test_reaches_the_test_site_and_previews_have_their_own_site()
    {
        foreach (var path in Workflow.Files().Concat(Repository.Files(Path.Combine("eng", "deploy"), "*.sh")))
        {
            var text = File.ReadAllText(path);
            var name = Repository.Relative(path);
            if (name is ".github/workflows/deploy-test.yml" or "eng/deploy/apply-infra.sh")
            {
                continue;
            }

            text.ShouldNotContain("rg-thoughttree-test", customMessage: $"{name}: only deploy-test.yml deploys to the test site");
            text.ShouldNotContain("stapp-thoughttree-test", customMessage: $"{name}: only deploy-test.yml deploys to the test site");
        }

        var previewJobs = Workflow.All().SelectMany(w => w.Jobs).Where(j => j.Environment == "preview").ToList();
        previewJobs.ShouldNotBeEmpty("previews are deployed by jobs in the GitHub environment 'preview'");
        previewJobs.ShouldAllBe(j => j.Workflow.Triggers.All(t => t == "workflow_run" || t == "schedule" || t == "workflow_dispatch"));
    }

    // Samples for the parser: every way of writing a pull request trigger and an id-token grant is found.
    [Theory]
    [InlineData("on: pull_request\njobs:\n  a:\n    permissions:\n      id-token: write\n")]
    [InlineData("on: [push, pull_request]\njobs:\n  a:\n    permissions: write-all\n")]
    [InlineData("on:\n  pull_request:\n    branches: [main]\npermissions:\n  id-token: write\njobs:\n  a:\n    runs-on: x\n")]
    [InlineData("'on': {pull_request: {}}\njobs:\n  a: {permissions: {id-token: write}}\n")]
    [InlineData("on: pull_request\njobs:\n  a:\n    runs-on: x\n")]
    public void A_pull_request_job_with_an_id_token_is_found(string yaml)
    {
        var workflow = Workflow.Parse(yaml);

        workflow.Triggers.Overlaps(PullRequestEvents).ShouldBeTrue();
        workflow.Jobs.SelectMany(OpenIdConnectJobs).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("on: pull_request\npermissions:\n  contents: read\njobs:\n  a:\n    runs-on: x\n")]
    [InlineData("on: [pull_request]\npermissions: {}\njobs:\n  a:\n    permissions:\n      contents: read\n      id-token: none\n")]
    public void A_pull_request_job_without_an_id_token_passes(string yaml)
    {
        Workflow.Parse(yaml).Jobs.SelectMany(OpenIdConnectJobs).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("    steps:\n      - run: git fetch origin \"$HEAD_SHA\" && git checkout FETCH_HEAD\n")]
    [InlineData("    steps:\n      - run: gh run download \"$RUN_ID\" && ./site/run.sh\n")]
    [InlineData("    steps:\n      - run: gh pr checkout 12\n")]
    [InlineData("    steps:\n      - run: git switch feature\n")]
    [InlineData("    steps:\n      - uses: actions/download-artifact@0123456789012345678901234567890123456789\n")]
    [InlineData("    steps:\n      - run: curl -L \"$URL/actions/artifacts/1/zip\" -o a.zip\n")]
    public void A_workflow_run_job_that_takes_the_triggering_code_is_found(string steps)
    {
        var workflow = Workflow.Parse("on:\n  workflow_run:\n    workflows: [x]\npermissions: {}\njobs:\n  a:\n    runs-on: x\n" + steps);
        WorkflowRunViolations(workflow).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("    if: github.event.workflow_run.conclusion == 'success'\n")]
    [InlineData("")]
    public void A_workflow_run_job_without_the_fork_filter_is_found(string condition)
    {
        var workflow = Workflow.Parse("on:\n  workflow_run:\n    workflows: [x]\npermissions: {}\njobs:\n  a:\n    environment: preview\n    permissions:\n      id-token: write\n" + condition);
        ForkFilterViolations(workflow).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("on: merge_group\npermissions: {}\njobs:\n  a:\n    environment: test\n    permissions:\n      id-token: write\n")]
    [InlineData("on: [merge_group]\npermissions: {}\njobs:\n  a:\n    permissions: write-all\n")]
    public void A_merge_group_job_with_an_id_token_is_found(string yaml)
    {
        var workflow = Workflow.Parse(yaml);
        workflow.Triggers.Overlaps(PullRequestEvents).ShouldBeTrue();
        workflow.Jobs.SelectMany(OpenIdConnectJobs).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("on: push\npermissions: {}\njobs:\n  a:\n    permissions:\n      id-token: write\n")]
    [InlineData("on: push\npermissions: {}\njobs:\n  a:\n    permissions:\n      id-token: write\n    uses: other/repo/.github/workflows/x.yml@0123456789012345678901234567890123456789\n")]
    [InlineData("on: push\npermissions: {}\njobs:\n  a:\n    uses: other/repo/.github/workflows/x.yml@0123456789012345678901234567890123456789\n")]
    public void A_credential_job_outside_an_environment_or_a_remote_workflow_is_found(string yaml)
    {
        CredentialJobViolations(Workflow.Parse(yaml)).ShouldNotBeEmpty();
    }

    internal static IEnumerable<string> WorkflowRunViolations(Workflow workflow)
    {
        foreach (var job in workflow.Jobs)
        {
            foreach (var step in job.Steps)
            {
                if (step.Uses?.StartsWith("actions/download-artifact@", StringComparison.Ordinal) == true)
                {
                    yield return $"{job}: actions/download-artifact (use eng/deploy/site_artifact.py fetch)";
                }

                if (step.Run is { } run && TakesTriggeringCode().Match(run) is { Success: true } match)
                {
                    yield return $"{job}: {match.Value}";
                }
            }
        }
    }

    internal static IEnumerable<string> ForkFilterViolations(Workflow workflow) =>
        workflow.Jobs
            .Where(j => j.CanRequestIdToken)
            .Where(j => Workflow.Scalar(Workflow.Child(j.Node, "if")) is not { } condition
                || !(condition.Contains(ForkFilter, StringComparison.Ordinal) || condition.Contains("github.event_name != 'workflow_run'", StringComparison.Ordinal)))
            .Select(j => $"{j}: no fork filter");

    internal static IEnumerable<string> CredentialJobViolations(Workflow workflow)
    {
        foreach (var job in workflow.Jobs)
        {
            if (job.Calls is { } called && !called.StartsWith("./", StringComparison.Ordinal))
            {
                yield return $"{job}: calls the remote workflow {called}";
            }

            if (job.Calls is null && job.CanRequestIdToken && job.Environment is null)
            {
                yield return $"{job}: id-token: write outside a GitHub environment";
            }
        }
    }

    private static IEnumerable<string> OpenIdConnectJobs(WorkflowJob job) => OpenIdConnectJobs(job, []);

    private static IEnumerable<string> OpenIdConnectJobs(WorkflowJob job, HashSet<string> visited)
    {
        if (job.CanRequestIdToken)
        {
            yield return $"{job}: id-token: write";
        }

        // A called reusable workflow runs with the caller's trigger; follow local calls to any depth. Remote calls
        // are refused by Every_job_with_an_openid_connect_token_runs_in_an_environment_and_calls_no_remote_workflow.
        if (job.Calls is { } called && called.StartsWith("./", StringComparison.Ordinal) && visited.Add(called))
        {
            var target = Repository.PathOf(called[2..].Split('/'));
            foreach (var inner in Workflow.Load(target).Jobs.SelectMany(j => OpenIdConnectJobs(j, visited)))
            {
                yield return $"{job} calls {inner}";
            }
        }
    }

    [GeneratedRegex(@"\bgit\s+(fetch|checkout|switch|pull|worktree)\b|\bgh\s+(pr\s+checkout|run\s+download|api\b[^\n]*artifacts)|/actions/artifacts/[^\s]*zip", RegexOptions.CultureInvariant)]
    private static partial Regex TakesTriggeringCode();
}
