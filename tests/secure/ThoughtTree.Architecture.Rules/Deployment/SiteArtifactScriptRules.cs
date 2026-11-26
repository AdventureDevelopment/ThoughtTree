using System.Diagnostics;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Deployment;

/// <summary>
/// ADR-046: eng/deploy/site_artifact.py is the only check between a build artifact and a job that holds a
/// deployment identity (previews from pull requests; promotion of a deploy-test run to production). Its tests
/// (site_artifact_tests.py, Python standard library, recorded API answers and zip fixtures, no network) run here,
/// so the branch check fails when one of its checks is dropped.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-046")]
public sealed class SiteArtifactScriptRules
{
    [Fact]
    public async Task The_artifact_checks_of_site_artifact_py_hold()
    {
        var start = new ProcessStartInfo("python3")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Repository.Root,
        };
        foreach (var argument in new[] { "-I", Repository.PathOf("tests", "secure", "ThoughtTree.Architecture.Rules", "Deployment", "site_artifact_tests.py"), Repository.Root })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("python3 did not start");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(true);
        var log = await error.ConfigureAwait(true) + await output.ConfigureAwait(true);

        process.ExitCode.ShouldBe(0, log);
        log.ShouldContain("\nOK");

        const int TestsOn20261119 = 48;
        var ran = System.Text.RegularExpressions.Regex.Match(log, @"Ran (\d+) tests?");
        ran.Success.ShouldBeTrue(log);
        int.Parse(ran.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(TestsOn20261119, "tests of site_artifact.py were removed");
    }
}
