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

        var runs = production.Jobs.SelectMany(j => j.Steps).Select(s => s.Run ?? string.Empty).ToList();
        runs.ShouldNotContain(r => r.Contains("publish-client.sh", StringComparison.Ordinal) || r.Contains("dotnet ", StringComparison.Ordinal), "production does not build; it uploads the tested artifact");
        production.Jobs.SelectMany(j => j.Steps).ShouldNotContain(s => s.Uses != null && s.Uses.StartsWith("actions/setup-dotnet@", StringComparison.Ordinal));

        var deploy = production.Jobs.Single(j => j.Environment == "production");
        var fetch = deploy.Steps.Select(s => s.Run ?? string.Empty).SingleOrDefault(r => r.Contains("site_artifact.py fetch", StringComparison.Ordinal));
        fetch.ShouldNotBeNull("the production job fetches the tested artifact");
        fetch.ShouldContain("--workflow .github/workflows/deploy-test.yml");
        fetch.ShouldContain("--event push");
        fetch.ShouldContain("--branch main");
        fetch.ShouldContain("--expect-sha256 \"$VERIFIED_SHA256\"", customMessage: "the job the owner approves checks the digest shown before the approval");

        // --branch makes site_artifact.py prove the run's commit is on main (not a tag named main; re-review N-H1,
        // tested in site_artifact_tests.py); every fetch in production must use it.
        var fetches = production.Jobs.SelectMany(j => j.Steps).Select(s => s.Run ?? string.Empty).Where(r => r.Contains("site_artifact.py fetch", StringComparison.Ordinal)).ToList();
        fetches.Count.ShouldBe(2, "verify and deploy both fetch the tested artifact");
        fetches.ShouldAllBe(r => r.Contains("--branch main", StringComparison.Ordinal));
    }

    [Fact]
    public void Test_keeps_the_site_it_deployed_as_the_promotable_artifact()
    {
        var test = Test;
        var build = BuildJob(test);
        var upload = build.Steps.SingleOrDefault(s => s.Uses?.StartsWith("actions/upload-artifact@", StringComparison.Ordinal) == true);

        upload.ShouldNotBeNull("deploy-test keeps the published site");
        upload.With("name").ShouldStartWith("site-sha256-${{");
        upload.With("retention-days").ShouldBe("90", "rollback reaches back 90 days (infra/README.md)");

        test.Jobs.SelectMany(j => j.Steps).Count(s => s.Run?.Contains("publish-client.sh", StringComparison.Ordinal) == true).ShouldBe(1, "the site is published once");
        build.Steps.Select(s => s.Run ?? string.Empty).ShouldContain(r => r.Contains("publish-client.sh", StringComparison.Ordinal));

        // The deploy job uploads exactly the build job's artifact, fetched as data like production does.
        var deploy = test.Jobs.Single(j => j.Environment == "test");
        var runs = deploy.Steps.Select(s => s.Run ?? string.Empty).ToList();
        var fetched = runs.FindIndex(r => r.Contains("site_artifact.py fetch --same-run", StringComparison.Ordinal) && r.Contains("--expect-sha256 \"$BUILT_SHA256\"", StringComparison.Ordinal));
        var uploaded = runs.FindIndex(r => r.Contains("upload-site.sh", StringComparison.Ordinal));
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
        // Re-review N-L1: dotnet publish runs MSBuild targets, analyzers and generators of every package; a job
        // with id-token: write would hand them the OpenID Connect request token.
        var offenders = Workflow.All().SelectMany(w => w.Jobs).Where(j => j.CanRequestIdToken)
            .SelectMany(j => j.Steps.Where(Builds).Select(s => $"{j}: {s.Run ?? s.Uses}"))
            .ToList();

        offenders.ShouldBeEmpty("build in a job without credentials and hand the result over as an artifact");
    }

    private static bool Builds(WorkflowStep step) =>
        step.Uses?.StartsWith("actions/setup-dotnet@", StringComparison.Ordinal) == true
        || step.Run?.Contains("publish-client.sh", StringComparison.Ordinal) == true
        || step.Run?.Contains("dotnet ", StringComparison.Ordinal) == true
        || step.Run?.Contains("msbuild", StringComparison.OrdinalIgnoreCase) == true;

    private static WorkflowJob BuildJob(Workflow workflow) =>
        workflow.Jobs.Single(j => !j.CanRequestIdToken && j.Steps.Any(s => s.Uses?.StartsWith("actions/upload-artifact@", StringComparison.Ordinal) == true));

    [Fact]
    public void The_artifact_carries_the_policy_hash_filled_in_at_publish_time()
    {
        // The artifact is the published wwwroot after publish-client.sh wrote the import map hash into
        // staticwebapp.config.json (ADR-045); nothing changes the files between test and production.
        var upload = BuildJob(Test).Steps.Single(s => s.Uses?.StartsWith("actions/upload-artifact@", StringComparison.Ordinal) == true);
        upload.With("path").ShouldBe("${{ runner.temp }}/site/wwwroot");
        File.ReadAllText(Repository.PathOf("eng", "deploy", "publish-client.sh")).ShouldContain("{importmap-hash}");
        Production.Jobs.SelectMany(j => j.Steps).Select(s => s.Run ?? string.Empty)
            .ShouldNotContain(r => r.Contains("staticwebapp.config.json", StringComparison.Ordinal), "production uploads the files as tested");
    }
}
