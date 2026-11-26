using System.Text.Json;
using System.Text.Json.Nodes;
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
public sealed partial class DeployToolRules
{
    private static IReadOnlyList<string> ScriptsAndWorkflows =>
        Workflow.Files().Concat(Repository.Files(Path.Combine("eng", "deploy"), "*.sh")).ToList();

    [Fact]
    public void No_package_is_resolved_at_run_time()
    {
        var offenders = ScriptsAndWorkflows
            .SelectMany(file => UnlockedInstall().Matches(Code(file)).Select(match => $"{Repository.Relative(file)}: {match.Value.Trim()}"))
            .ToList();

        offenders.ShouldBeEmpty("no npx, npm install, npm exec, yarn or pnpm dlx: tools come from a committed lock file");

        foreach (var file in ScriptsAndWorkflows)
        {
            foreach (Match cleanInstall in NpmCi().Matches(Code(file)))
            {
                cleanInstall.Value.ShouldContain("--ignore-scripts", customMessage: $"{Repository.Relative(file)}: npm ci runs no install scripts");
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
        direct.Where(dependency => !ExactVersion().IsMatch(dependency.Value.GetString() ?? string.Empty)).Select(dependency => dependency.Name)
            .ShouldBeEmpty("exact versions only in eng/deploy/package.json");
        package.RootElement.TryGetProperty("devDependencies", out _).ShouldBeFalse();

        locked.RootElement.GetProperty("lockfileVersion").GetInt32().ShouldBe(3);
        var packages = locked.RootElement.GetProperty("packages").EnumerateObject().Where(lockedPackage => lockedPackage.Name.Length > 0).ToList();
        packages.ShouldNotBeEmpty();
        packages.Where(lockedPackage => !(lockedPackage.Value.TryGetProperty("integrity", out var integrity) && integrity.GetString()!.StartsWith("sha512-", StringComparison.Ordinal)))
            .Select(lockedPackage => lockedPackage.Name).ShouldBeEmpty("every locked package has a sha512 integrity hash");
        packages.Where(lockedPackage => !(lockedPackage.Value.TryGetProperty("resolved", out var resolved) && resolved.GetString()!.StartsWith("https://registry.npmjs.org/", StringComparison.Ordinal)))
            .Select(lockedPackage => lockedPackage.Name).ShouldBeEmpty("every locked package comes from the npm registry over https");

        var root = locked.RootElement.GetProperty("packages").GetProperty("").GetProperty("dependencies");
        foreach (var dependency in direct)
        {
            root.GetProperty(dependency.Name).GetString().ShouldBe(dependency.Value.GetString(), "lock file matches package.json");
        }
    }

    [Fact]
    public void Every_locked_licence_is_on_the_adr_050_list_or_approved_by_the_owner()
    {
        LicenceProblems(LockText, RecordText, PinText).ShouldBeEmpty("ADR-050: every licence outside the list needs the owner's dated approval in eng/deploy/licences.json");
    }

    [Theory]
    [InlineData("pending package", "awaiting owner approval")]
    [InlineData("rejected package", "status rejected")]
    [InlineData("undated package", "no approval date")]
    [InlineData("licence differs", "differs from the lock file")]
    [InlineData("unrecorded package", "not recorded")]
    [InlineData("new pin", "StaticSitesClient@")]
    [InlineData("pending client", "awaiting owner approval")]
    [InlineData("undated client", "no approval date")]
    [InlineData("list widened", "ADR-050 list")]
    public void A_missing_or_stale_licence_decision_is_found(string variant, string expected)
    {
        var record = JsonNode.Parse(RecordText)!.AsObject();
        var lockFile = JsonNode.Parse(LockText)!.AsObject();
        var pin = JsonNode.Parse(PinText)!.AsObject();
        var packages = record["packages"]!.AsObject();
        var first = packages.First().Key;
        var client = record["binaries"]!.AsObject().First().Value!.AsObject();
        switch (variant)
        {
            case "pending package": packages[first]!["status"] = "awaiting owner approval"; break;
            case "rejected package": packages[first]!["status"] = "rejected"; break;
            case "undated package": packages[first]!.AsObject().Remove("approved"); break;
            case "licence differs": packages[first]!["license"] = "MIT OR GPL-3.0"; break;
            case "unrecorded package": packages.Remove(first); break;
            case "new pin": pin["buildId"] = new string('c', 40); break;
            case "pending client": client["status"] = "awaiting owner approval"; break;
            case "undated client": client.Remove("approved"); break;
            case "list widened": record["approvedByAdr050"]!.AsArray().Add("GPL-3.0"); break;
            default: throw new ArgumentOutOfRangeException(nameof(variant));
        }

        var problems = LicenceProblems(lockFile.ToJsonString(), record.ToJsonString(), pin.ToJsonString());
        problems.ShouldNotBeEmpty(variant);
        problems.ShouldContain(problem => problem.Contains(expected, StringComparison.Ordinal), variant);
    }

    private static string LockText => File.ReadAllText(Repository.PathOf("eng", "deploy", "package-lock.json"));

    private static string RecordText => File.ReadAllText(Repository.PathOf("eng", "deploy", "licences.json"));

    private static string PinText => File.ReadAllText(Repository.PathOf("eng", "deploy", "deploy-client.json"));

    private const string Approved = "approved by the owner";

    internal static List<string> LicenceProblems(string lockJson, string recordJson, string pinJson)
    {
        using var locked = JsonDocument.Parse(lockJson);
        using var record = JsonDocument.Parse(recordJson);
        using var pin = JsonDocument.Parse(pinJson);
        var problems = new List<string>();

        var approvedList = record.RootElement.GetProperty("approvedByAdr050").EnumerateArray().Select(approvedLicence => approvedLicence.GetString()).ToHashSet(StringComparer.Ordinal);
        if (!approvedList.SetEquals(["MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "ISC"]))
        {
            problems.Add("approvedByAdr050 differs from the ADR-050 list (MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC)");
        }

        var recorded = record.RootElement.GetProperty("packages").EnumerateObject().ToDictionary(recordedPackage => recordedPackage.Name, recordedPackage => recordedPackage.Value.Clone(), StringComparer.Ordinal);
        foreach (var package in locked.RootElement.GetProperty("packages").EnumerateObject().Where(lockedPackage => lockedPackage.Name.Length > 0))
        {
            var licence = package.Value.TryGetProperty("license", out var licenceElement) ? licenceElement.GetString() : null;
            var name = package.Name[(package.Name.LastIndexOf("node_modules/", StringComparison.Ordinal) + "node_modules/".Length)..] + "@" + package.Value.GetProperty("version").GetString();
            if (licence is not null && approvedList.Contains(licence))
            {
                continue;
            }

            if (!recorded.TryGetValue(name, out var entry))
            {
                problems.Add($"{name} ({licence ?? "no licence"}): not recorded in eng/deploy/licences.json");
                continue;
            }

            var noted = entry.GetProperty("license").ValueKind == JsonValueKind.Null ? null : entry.GetProperty("license").GetString();
            if (noted != licence)
            {
                problems.Add($"{name}: recorded licence {noted ?? "none"} differs from the lock file's {licence ?? "none"}");
            }

            problems.AddRange(DecisionProblems(name, entry));
        }

        var approvalCoversOnlyThePinnedClient = "StaticSitesClient@" + pin.RootElement.GetProperty("buildId").GetString();
        var binaries = record.RootElement.GetProperty("binaries").EnumerateObject().ToList();
        if (binaries.Count != 1 || binaries[0].Name != approvalCoversOnlyThePinnedClient)
        {
            problems.Add($"binaries must record exactly {approvalCoversOnlyThePinnedClient}, the build pinned in eng/deploy/deploy-client.json; found {string.Join(", ", binaries.Select(binary => binary.Name))}");
        }

        problems.AddRange(binaries.SelectMany(binary => DecisionProblems(binary.Name, binary.Value)));
        return problems;
    }

    private static IEnumerable<string> DecisionProblems(string name, JsonElement entry)
    {
        var status = entry.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
        if (status != Approved)
        {
            yield return $"{name}: status {status ?? "missing"}; only \"{Approved}\" lets it into the lock file";
        }

        if (!entry.TryGetProperty("approved", out var date) || !DateOnly.TryParseExact(date.GetString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
        {
            yield return $"{name}: no approval date (yyyy-MM-dd)";
        }
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
        var code = string.Join('\n', upload.Split('\n').Where(line => !line.TrimStart().StartsWith('#')));

        var logout = code.IndexOf("az logout", StringComparison.Ordinal);
        var deploy = code.IndexOf(" deploy \"$folder\"", StringComparison.Ordinal);
        logout.ShouldBeGreaterThan(0, "the Azure CLI signs out before the deploy tools run");
        deploy.ShouldBeGreaterThan(logout, "the deploy tools run after the Azure sign-out");
        code[deploy..].ShouldNotContain("az ", customMessage: "no Azure CLI after the sign-out");
        code.ShouldContain("unset ACTIONS_ID_TOKEN_REQUEST_URL ACTIONS_ID_TOKEN_REQUEST_TOKEN");
        code.ShouldContain("export SWA_CLI_DEPLOYMENT_TOKEN=");

        ScriptsAndWorkflows.Where(file => File.ReadAllText(file).Contains("--deployment-token", StringComparison.Ordinal)).Select(Repository.Relative)
            .ShouldBeEmpty("the deployment token is never on a command line");
    }

    [Fact]
    public void The_sign_in_puts_no_token_on_a_command_line_where_other_processes_can_read_it()
    {
        var code = Code(Repository.PathOf("eng", "deploy", "azure-sign-in.sh"));

        SecretOnCommandLine().Matches(code).Select(match => match.Value)
            .ShouldBeEmpty("the request token goes to curl as --header @file, the OpenID Connect token to az as --federated-token @file");
        code.ShouldContain("--federated-token \"@");
        code.ShouldContain("--header \"@");
        code.ShouldContain("umask 077");
    }

    [Theory]
    [InlineData("az login --service-principal -u x --federated-token \"$token\"")]
    [InlineData("az login --federated-token $(cat t)")]
    [InlineData("az login --federated-token=$token")]
    [InlineData("az login --federated-token=\"$(cat t)\"")]
    [InlineData("curl --header \"Authorization: bearer ${ACTIONS_ID_TOKEN_REQUEST_TOKEN}\" \"$URL\"")]
    [InlineData("curl -H \"Authorization: Bearer $ACTIONS_ID_TOKEN_REQUEST_TOKEN\" x")]
    public void A_token_on_a_command_line_is_found(string line)
    {
        SecretOnCommandLine().IsMatch(line).ShouldBeTrue();
    }

    [Fact]
    public void Deploy_tools_are_installed_before_the_azure_sign_in()
    {
        var jobs = Workflow.All().SelectMany(workflow => workflow.Jobs)
            .Where(job => job.Steps.Any(step => step.Run?.Contains("upload-site.sh", StringComparison.Ordinal) == true || step.Run?.Contains("preview.sh deploy", StringComparison.Ordinal) == true))
            .ToList();
        jobs.ShouldNotBeEmpty();

        foreach (var job in jobs)
        {
            var steps = job.Steps.Select(step => step.Run ?? string.Empty).ToList();
            var install = steps.FindIndex(command => command.Contains("eng/deploy/install-tools.sh", StringComparison.Ordinal));
            var signIn = steps.FindIndex(command => command.Contains("azure-sign-in.sh", StringComparison.Ordinal));
            install.ShouldBeGreaterThanOrEqualTo(0, $"{job}: installs the locked deploy tools");
            signIn.ShouldBeGreaterThan(install, $"{job}: installs the deploy tools before signing in to Azure");
        }
    }

    /// <summary>The file without comment lines, so that a comment can neither break nor satisfy a rule.</summary>
    private static string Code(string path) =>
        string.Join('\n', File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith('#')));

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

    [GeneratedRegex(@"--federated-token(\s+|=)(?!""?@)\S+|(--header|-H)\s+""?Authorization:[^\n]*\$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
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
