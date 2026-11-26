using System.Diagnostics;
using System.Text.RegularExpressions;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Deployment;

/// <summary>
/// ADR-046, owner decision 2026-11-24: previews of closed pull requests are deleted with
/// `az staticwebapp environment delete`, whose only tolerated failure is the known polling error (AuthorizationFailed
/// for 'Microsoft.Web/locations/staticSitesOperationStatuses/read' at subscription scope, observed for pr13 after
/// Azure had accepted the DELETE); every delete is checked on the environment list. No REST call replaces it: the
/// az rest ban of InfrastructureRules stays as it is. preview_prune_tests.py runs preview.sh against a stub az.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-046")]
public sealed partial class PreviewPruneRules
{
    private const string KnownAction = "'Microsoft.Web/locations/staticSitesOperationStatuses/read'";

    private static string PreviewScript => string.Join('\n', File.ReadAllLines(Repository.PathOf("eng", "deploy", "preview.sh")).Where(line => !line.TrimStart().StartsWith('#')));

    [Fact]
    public async Task Prune_behaves_as_decided_against_a_stub_az()
    {
        var start = new ProcessStartInfo("python3")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Repository.Root,
        };
        foreach (var argument in new[] { "-I", Repository.PathOf("tests", "secure", "ThoughtTree.Architecture.Rules", "Deployment", "preview_prune_tests.py"), Repository.Root })
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
        var ran = TestsRan().Match(log);
        ran.Success.ShouldBeTrue(log);
        int.Parse(ran.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(11, "tests of preview.sh prune were removed");
    }

    [Fact]
    public void Only_the_known_polling_error_is_tolerated_and_every_delete_is_checked_on_the_list()
    {
        var script = PreviewScript;

        script.ShouldContain("az staticwebapp environment delete", customMessage: "the owner's choice: the normal delete command");
        script.ShouldContain($"\"perform action {KnownAction}\"", customMessage: "the tolerance names exactly the known action");
        script.ShouldContain("Code: AuthorizationFailed", customMessage: "and the known error code");
        script.ShouldContain("staticSitesOperationStatuses/[0-9a-zA-Z-]+'", customMessage: "and the subscription-scope shape of the operation status");
        script.ShouldContain("Previews still listed after the wait", customMessage: "every delete is checked on the environment list");
        BroadTolerance().Matches(script).Select(match => match.Value).ShouldBeEmpty("no || true, || : or 2>/dev/null on the delete");
    }

    [Fact]
    public void No_rest_call_in_the_deploy_scripts()
    {
        Repository.Files(Path.Combine("eng", "deploy"), "*.sh")
            .Where(script => RestCall().IsMatch(File.ReadAllText(script)))
            .Select(Repository.Relative)
            .ShouldBeEmpty("az rest stays banned in eng/deploy (owner, 2026-11-24: we have that rule for a reason)");
    }

    [Theory]
    [InlineData("az staticwebapp environment delete --name x --yes || true")]
    [InlineData("az staticwebapp environment delete --name x --yes || :")]
    [InlineData("az staticwebapp environment delete --name x --yes 2>/dev/null")]
    [InlineData("az staticwebapp environment delete \\\n  --environment-name \"$1\" --yes --output none 2>&1 || true")]
    public void A_broad_tolerance_on_the_delete_is_found(string line)
    {
        BroadTolerance().IsMatch(line).ShouldBeTrue();
    }

    [GeneratedRegex(@"az\s+staticwebapp\s+environment\s+delete\b[^\n]*(\\\n[^\n]*)*?(\|\|\s*(true\b|:(?!\S))|2>\s*/dev/null)", RegexOptions.CultureInvariant)]
    private static partial Regex BroadTolerance();

    [GeneratedRegex(@"\baz\s+rest\b", RegexOptions.CultureInvariant)]
    private static partial Regex RestCall();

    [GeneratedRegex(@"Ran (\d+) tests?", RegexOptions.CultureInvariant)]
    private static partial Regex TestsRan();
}
