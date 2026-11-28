using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Deployment;

/// <summary>
/// ADR-046 (promotion to production is a manual approval step; test and production identical apart from
/// parameters): production receives exactly the site that the test environment received. deploy-test publishes
/// once and keeps the site as an artifact; deploy-production builds nothing, takes a deploy-test run id, checks
/// that run and the SHA-256 of its artifact, and uploads those files.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-046")]
public sealed class PromotionRules
{
    private static Workflow Production => Workflow.Load(Repository.PathOf(".github", "workflows", "deploy-production.yml"));

    private static Workflow Test => Workflow.Load(Repository.PathOf(".github", "workflows", "deploy-test.yml"));

    [Fact]
    public void Production_builds_nothing_and_promotes_a_test_run()
    {
        var production = Production;

        var inputs = Workflow.Child(Workflow.Child(Workflow.Child(production.Root, "on"), "workflow_dispatch"), "inputs");
        Workflow.Scalar(Workflow.Child(Workflow.Child(inputs, "test_run_id"), "required")).ShouldBe("true", "the run id of the tested build is a required input");

        var runs = production.Jobs.SelectMany(job => job.Steps).Select(step => step.Run ?? string.Empty).ToList();
        runs.ShouldNotContain(run => run.Contains("publish-client.sh", StringComparison.Ordinal) || run.Contains("dotnet ", StringComparison.Ordinal), "production does not build; it uploads the tested artifact");
        production.Jobs.SelectMany(job => job.Steps).ShouldNotContain(step => step.Uses != null && step.Uses.StartsWith("actions/setup-dotnet@", StringComparison.Ordinal));

        var deploy = production.Jobs.Single(job => job.Environment == "production");
        var fetch = deploy.Steps.Select(step => step.Run ?? string.Empty).SingleOrDefault(run => run.Contains("site_artifact.py fetch", StringComparison.Ordinal));
        fetch.ShouldNotBeNull("the production job fetches the tested artifact");
        fetch.ShouldContain("--workflow .github/workflows/deploy-test.yml");
        fetch.ShouldContain("--event push");
        fetch.ShouldContain("--branch main", customMessage: "proves the run's commit is on main, not on a tag named main");
        fetch.ShouldContain("--expect-sha256 \"$VERIFIED_SHA256\"", customMessage: "the job the owner approves checks the digest shown before the approval");

        var fetches = production.Jobs.SelectMany(job => job.Steps).Select(step => step.Run ?? string.Empty).Where(run => run.Contains("site_artifact.py fetch", StringComparison.Ordinal)).ToList();
        fetches.Count.ShouldBe(2, "verify and deploy both fetch the tested artifact");
        fetches.ShouldAllBe(run => run.Contains("--branch main", StringComparison.Ordinal));
    }

    [Fact]
    public void Test_keeps_the_site_it_deployed_as_the_promotable_artifact()
    {
        var test = Test;
        var build = BuildJob(test);
        var upload = build.Steps.SingleOrDefault(step => step.Uses?.StartsWith("actions/upload-artifact@", StringComparison.Ordinal) == true);

        upload.ShouldNotBeNull("deploy-test keeps the published site");
        upload.With("name").ShouldStartWith("site-sha256-${{");
        upload.With("retention-days").ShouldBe("90", "rollback reaches back 90 days (infra/README.md)");

        test.Jobs.SelectMany(job => job.Steps).Count(step => step.Run?.Contains("publish-client.sh", StringComparison.Ordinal) == true).ShouldBe(1, "the site is published once");
        build.Steps.Select(step => step.Run ?? string.Empty).ShouldContain(run => run.Contains("publish-client.sh", StringComparison.Ordinal));

        var deploy = test.Jobs.Single(job => job.Environment == "test");
        var runs = deploy.Steps.Select(step => step.Run ?? string.Empty).ToList();
        var fetched = runs.FindIndex(run => run.Contains("site_artifact.py fetch --same-run", StringComparison.Ordinal) && run.Contains("--expect-sha256 \"$BUILT_SHA256\"", StringComparison.Ordinal));
        var uploaded = runs.FindIndex(run => run.Contains("upload-site.sh", StringComparison.Ordinal));
        fetched.ShouldBeGreaterThanOrEqualTo(0, "the test deploy job fetches the build job's artifact and checks its digest");
        var fetch = runs[fetched];
        fetch.ShouldContain("--workflow .github/workflows/deploy-test.yml", customMessage: "third review N3-I3: the fetch names its own workflow file");
        fetch.ShouldContain("--event push");
        fetch.ShouldContain("--branch main", customMessage: "the test deploy, too, takes only a run whose commit is on main");
        uploaded.ShouldBeGreaterThan(fetched);
    }

    [Fact]
    public void No_job_that_holds_a_credential_builds_code()
    {
        var offenders = Workflow.All().SelectMany(workflow => workflow.Jobs).Where(job => job.CanRequestIdToken)
            .SelectMany(job => job.Steps.Where(Builds).Select(step => $"{job}: {step.Run ?? step.Uses}"))
            .ToList();

        offenders.ShouldBeEmpty("a build runs the MSBuild targets and analyzers of every package; build in a job without credentials and hand the result over as an artifact");
    }

    private static bool Builds(WorkflowStep step) =>
        step.Uses?.StartsWith("actions/setup-dotnet@", StringComparison.Ordinal) == true
        || step.Run?.Contains("publish-client.sh", StringComparison.Ordinal) == true
        || step.Run?.Contains("dotnet ", StringComparison.Ordinal) == true
        || step.Run?.Contains("msbuild", StringComparison.OrdinalIgnoreCase) == true;

    private static WorkflowJob BuildJob(Workflow workflow) =>
        workflow.Jobs.Single(job => !job.CanRequestIdToken && job.Steps.Any(step => step.Uses?.StartsWith("actions/upload-artifact@", StringComparison.Ordinal) == true));

    [Fact]
    public void The_artifact_carries_the_policy_hash_filled_in_at_publish_time()
    {
        var upload = BuildJob(Test).Steps.Single(step => step.Uses?.StartsWith("actions/upload-artifact@", StringComparison.Ordinal) == true);
        upload.With("path").ShouldBe("${{ runner.temp }}/site/wwwroot");
        File.ReadAllText(Repository.PathOf("eng", "deploy", "publish-client.sh")).ShouldContain("{importmap-hash}");
        Production.Jobs.SelectMany(job => job.Steps).Select(step => step.Run ?? string.Empty)
            .ShouldNotContain(run => run.Contains("staticwebapp.config.json", StringComparison.Ordinal), "production uploads the files as tested");
    }
}
