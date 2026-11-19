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
public class PullRequestIsolationRules
{
    private static readonly string[] PullRequestEvents = ["pull_request", "pull_request_target", "pull_request_review", "pull_request_review_comment", "issue_comment"];

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

    private static IEnumerable<string> OpenIdConnectJobs(WorkflowJob job)
    {
        if (job.CanRequestIdToken)
        {
            yield return $"{job}: id-token: write";
        }

        // A called reusable workflow in this repository runs with the caller's trigger; check its jobs too.
        if (job.Calls is { } called && called.StartsWith("./", StringComparison.Ordinal))
        {
            var target = Repository.PathOf(called[2..].Split('/'));
            foreach (var inner in Workflow.Load(target).Jobs.Where(j => j.CanRequestIdToken))
            {
                yield return $"{job} calls {inner}: id-token: write";
            }
        }
    }
}
