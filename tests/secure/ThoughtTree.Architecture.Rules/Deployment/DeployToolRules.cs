using System.Text.Json;
using System.Text.RegularExpressions;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Deployment;

/// <summary>
/// ADR-046 and ADR-050: the tools that run next to the deployment credential are exactly the ones recorded in the
/// repository. The Static Web Apps CLI comes from eng/deploy/package-lock.json (exact version, sha512 integrity,
/// no install scripts); its deploy client is pinned by SHA-256; both are installed before the Azure sign-in;
/// the upload runs after the Azure CLI has signed out and gets the token only through the environment.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-046")]
[Trait("ADR", "ADR-050")]
public partial class DeployToolRules
{
    private static IReadOnlyList<string> ScriptsAndWorkflows =>
        Workflow.Files().Concat(Repository.Files(Path.Combine("eng", "deploy"), "*.sh")).ToList();

    [Fact]
    public void No_package_is_resolved_at_run_time()
    {
        var offenders = ScriptsAndWorkflows
            .SelectMany(f => UnlockedInstall().Matches(Code(f)).Select(m => $"{Repository.Relative(f)}: {m.Value.Trim()}"))
            .ToList();

        offenders.ShouldBeEmpty("no npx, npm install, npm exec, yarn or pnpm dlx: tools come from a committed lock file");

        foreach (var file in ScriptsAndWorkflows)
        {
            foreach (Match ci in NpmCi().Matches(Code(file)))
            {
                ci.Value.ShouldContain("--ignore-scripts", customMessage: $"{Repository.Relative(file)}: npm ci runs no install scripts");
            }
        }
    }

    [Fact]
    public void The_deploy_tools_are_locked_with_exact_versions_and_integrity_hashes()
    {
        using var package = JsonDocument.Parse(File.ReadAllText(Repository.PathOf("eng", "deploy", "package.json")));
        using var locked = JsonDocument.Parse(File.ReadAllText(Repository.PathOf("eng", "deploy", "package-lock.json")));

        var direct = package.RootElement.GetProperty("dependencies").EnumerateObject().ToList();
        direct.ShouldNotBeEmpty();
        direct.Where(d => !ExactVersion().IsMatch(d.Value.GetString() ?? string.Empty)).Select(d => d.Name)
            .ShouldBeEmpty("exact versions only in eng/deploy/package.json");
        package.RootElement.TryGetProperty("devDependencies", out _).ShouldBeFalse();

        locked.RootElement.GetProperty("lockfileVersion").GetInt32().ShouldBe(3);
        var packages = locked.RootElement.GetProperty("packages").EnumerateObject().Where(p => p.Name.Length > 0).ToList();
        packages.ShouldNotBeEmpty();
        packages.Where(p => !(p.Value.TryGetProperty("integrity", out var i) && i.GetString()!.StartsWith("sha512-", StringComparison.Ordinal)))
            .Select(p => p.Name).ShouldBeEmpty("every locked package has a sha512 integrity hash");
        packages.Where(p => !(p.Value.TryGetProperty("resolved", out var r) && r.GetString()!.StartsWith("https://registry.npmjs.org/", StringComparison.Ordinal)))
            .Select(p => p.Name).ShouldBeEmpty("every locked package comes from the npm registry over https");

        var root = locked.RootElement.GetProperty("packages").GetProperty("").GetProperty("dependencies");
        foreach (var dependency in direct)
        {
            root.GetProperty(dependency.Name).GetString().ShouldBe(dependency.Value.GetString(), "lock file matches package.json");
        }
    }

    [Fact]
    public void Every_locked_licence_is_on_the_adr_050_list_or_recorded_for_the_owner()
    {
        // Re-review N-L2: ADR-050 needs a human decision for any licence not on its list, and for none at all.
        using var locked = JsonDocument.Parse(File.ReadAllText(Repository.PathOf("eng", "deploy", "package-lock.json")));
        using var record = JsonDocument.Parse(File.ReadAllText(Repository.PathOf("eng", "deploy", "licences.json")));
        var approved = record.RootElement.GetProperty("approvedByAdr050").EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal);
        approved.SetEquals(["MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "ISC"]).ShouldBeTrue("the list of ADR-050, unchanged");
        var recorded = record.RootElement.GetProperty("packages").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetProperty("license").ValueKind == JsonValueKind.Null ? null : p.Value.GetProperty("license").GetString(), StringComparer.Ordinal);

        var offenders = new List<string>();
        foreach (var package in locked.RootElement.GetProperty("packages").EnumerateObject().Where(p => p.Name.Length > 0))
        {
            var licence = package.Value.TryGetProperty("license", out var l) ? l.GetString() : null;
            var name = package.Name[(package.Name.LastIndexOf("node_modules/", StringComparison.Ordinal) + "node_modules/".Length)..] + "@" + package.Value.GetProperty("version").GetString();
            if (licence is not null && approved.Contains(licence))
            {
                continue;
            }

            if (!recorded.TryGetValue(name, out var noted) || noted != licence)
            {
                offenders.Add($"{name}: {licence ?? "no licence"}");
            }
        }

        offenders.ShouldBeEmpty("record the package in eng/deploy/licences.json for the owner's decision (ADR-050)");
        record.RootElement.GetProperty("binaries").EnumerateObject().Select(b => b.Name)
            .ShouldContain(n => n.StartsWith("StaticSitesClient@", StringComparison.Ordinal), "the pinned deploy client's licence is recorded too");
    }

    [Fact]
    public void The_deploy_client_is_pinned_by_sha256()
    {
        using var pin = JsonDocument.Parse(File.ReadAllText(Repository.PathOf("eng", "deploy", "deploy-client.json")));
        Hex40().IsMatch(pin.RootElement.GetProperty("buildId").GetString()!).ShouldBeTrue();
        Hex64().IsMatch(pin.RootElement.GetProperty("sha256").GetString()!).ShouldBeTrue();
        pin.RootElement.GetProperty("url").GetString()!.ShouldStartWith("https://");

        File.ReadAllText(Repository.PathOf("eng", "deploy", "install-tools.sh")).ShouldContain("sha256sum --check --strict");
        File.ReadAllText(Repository.PathOf("eng", "deploy", "upload-site.sh")).ShouldContain("sha256sum --check --strict");
    }

    [Fact]
    public void The_upload_runs_with_only_the_deployment_token()
    {
        var upload = File.ReadAllText(Repository.PathOf("eng", "deploy", "upload-site.sh"));
        var code = string.Join('\n', upload.Split('\n').Where(l => !l.TrimStart().StartsWith('#')));

        var logout = code.IndexOf("az logout", StringComparison.Ordinal);
        var deploy = code.IndexOf(" deploy \"$folder\"", StringComparison.Ordinal);
        logout.ShouldBeGreaterThan(0, "the Azure CLI signs out before the deploy tools run");
        deploy.ShouldBeGreaterThan(logout, "the deploy tools run after the Azure sign-out");
        code[deploy..].ShouldNotContain("az ", customMessage: "no Azure CLI after the sign-out");
        code.ShouldContain("unset ACTIONS_ID_TOKEN_REQUEST_URL ACTIONS_ID_TOKEN_REQUEST_TOKEN");
        code.ShouldContain("export SWA_CLI_DEPLOYMENT_TOKEN=");

        ScriptsAndWorkflows.Where(f => File.ReadAllText(f).Contains("--deployment-token", StringComparison.Ordinal)).Select(Repository.Relative)
            .ShouldBeEmpty("the deployment token is never on a command line");
    }

    [Fact]
    public void The_sign_in_puts_no_token_on_a_command_line()
    {
        // First review L3, re-review: argv of every process is readable by the job's other processes in /proc.
        var code = Code(Repository.PathOf("eng", "deploy", "azure-sign-in.sh"));

        SecretOnCommandLine().Matches(code).Select(m => m.Value)
            .ShouldBeEmpty("the request token goes to curl as --header @file, the OpenID Connect token to az as --federated-token @file");
        code.ShouldContain("--federated-token \"@");
        code.ShouldContain("--header \"@");
        code.ShouldContain("umask 077");
    }

    [Theory]
    [InlineData("az login --service-principal -u x --federated-token \"$token\"")]
    [InlineData("az login --federated-token $(cat t)")]
    [InlineData("curl --header \"Authorization: bearer ${ACTIONS_ID_TOKEN_REQUEST_TOKEN}\" \"$URL\"")]
    [InlineData("curl -H \"Authorization: Bearer $ACTIONS_ID_TOKEN_REQUEST_TOKEN\" x")]
    public void A_token_on_a_command_line_is_found(string line)
    {
        SecretOnCommandLine().IsMatch(line).ShouldBeTrue();
    }

    [Fact]
    public void Deploy_tools_are_installed_before_the_azure_sign_in()
    {
        var jobs = Workflow.All().SelectMany(w => w.Jobs)
            .Where(j => j.Steps.Any(s => s.Run?.Contains("upload-site.sh", StringComparison.Ordinal) == true || s.Run?.Contains("preview.sh deploy", StringComparison.Ordinal) == true))
            .ToList();
        jobs.ShouldNotBeEmpty();

        foreach (var job in jobs)
        {
            var steps = job.Steps.Select(s => s.Run ?? string.Empty).ToList();
            var install = steps.FindIndex(r => r.Contains("eng/deploy/install-tools.sh", StringComparison.Ordinal));
            var signIn = steps.FindIndex(r => r.Contains("azure-sign-in.sh", StringComparison.Ordinal));
            install.ShouldBeGreaterThanOrEqualTo(0, $"{job}: installs the locked deploy tools");
            signIn.ShouldBeGreaterThan(install, $"{job}: installs the deploy tools before signing in to Azure");
        }
    }

    /// <summary>The file without comment lines, so that a comment can neither break nor satisfy a rule.</summary>
    private static string Code(string path) =>
        string.Join('\n', File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith('#')));

    [Theory]
    [InlineData("npx --yes @azure/static-web-apps-cli@2.1.0 deploy")]
    [InlineData("run: npm install -g x")]
    [InlineData("npm i left-pad")]
    [InlineData("npm exec swa")]
    [InlineData("pnpm dlx swa")]
    [InlineData("yarn dlx swa")]
    public void A_run_time_package_resolution_is_found(string text)
    {
        UnlockedInstall().IsMatch(text).ShouldBeTrue();
    }

    [GeneratedRegex(@"\bnpx\b|\bnpm\s+(install|i|add|exec|x)\b|\b(pnpm|yarn)\s+dlx\b|\byarn\s+add\b", RegexOptions.CultureInvariant)]
    private static partial Regex UnlockedInstall();

    [GeneratedRegex(@"--federated-token\s+(?!""@)\S+|(--header|-H)\s+""?Authorization:[^\n]*\$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SecretOnCommandLine();

    [GeneratedRegex(@"\bnpm\s+ci\b[^\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex NpmCi();

    [GeneratedRegex(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactVersion();

    [GeneratedRegex(@"^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex Hex40();

    [GeneratedRegex(@"^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Hex64();
}
