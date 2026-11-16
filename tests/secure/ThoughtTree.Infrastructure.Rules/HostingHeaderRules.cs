using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ThoughtTree.Infrastructure.Rules;

/// <summary>
/// ADR-045: the Static Web Apps configuration sends a strict Content Security Policy and
/// Referrer-Policy as HTTP headers, and the page needs nothing the policy forbids. The check
/// against the deployed site runs after deployment (the deploy workflow's smoke step).
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-045")]
public partial class HostingHeaderRules
{
    private static readonly string WwwRoot = Repository.PathOf("src", "ThoughtTree.Client", "wwwroot");

    private static readonly JsonObject Headers =
        JsonNode.Parse(File.ReadAllText(Path.Combine(WwwRoot, "staticwebapp.config.json")))!["globalHeaders"]!.AsObject();

    private static Dictionary<string, string[]> Policy() =>
        ((string)Headers["Content-Security-Policy"]!).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(p => p[0], p => p[1..]);

    [Fact]
    public void Policy_allows_only_the_own_origin()
    {
        var policy = Policy();

        policy["default-src"].ShouldBe(["'none'"]);
        foreach (var (directive, sources) in policy.Where(p => p.Key != "default-src"))
        {
            sources.ShouldAllBe(s => s == "'self'" || s == "'none'" || s == "'wasm-unsafe-eval'" || s == "data:",
                $"{directive}: own origin only, no third parties (ADR-045)");
        }

        policy["frame-ancestors"].ShouldBe(["'none'"]);
        policy["object-src"].ShouldBe(["'none'"]);
    }

    [Fact]
    public void Policy_is_not_weakened_beyond_what_blazor_needs()
    {
        var csp = (string)Headers["Content-Security-Policy"]!;

        csp.ShouldNotContain("'unsafe-inline'");
        Regex.IsMatch(csp, @"(?<!wasm-)'unsafe-eval'").ShouldBeFalse("only 'wasm-unsafe-eval' is allowed (ADR-045)");
        Policy()["script-src"].ShouldBe(["'self'", "'wasm-unsafe-eval'"]);
    }

    [Fact]
    public void Referrer_is_never_sent()
    {
        ((string)Headers["Referrer-Policy"]!).ShouldBe("no-referrer");
    }

    [Fact]
    public void Start_page_has_no_inline_script_and_loads_nothing_from_elsewhere()
    {
        var html = File.ReadAllText(Path.Combine(WwwRoot, "index.html"));

        ScriptTag().Matches(html).ShouldAllBe(m => m.Groups["attrs"].Value.Contains("src=", StringComparison.Ordinal),
            "an inline script would be blocked by script-src 'self'");
        html.ShouldNotContain("style=", Case.Insensitive, "inline styles would be blocked by style-src 'self'");
        ExternalUrl().IsMatch(html).ShouldBeFalse("nothing from third parties (ADR-045)");
    }

    [GeneratedRegex(@"<script(?<attrs>[^>]*)>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTag();

    [GeneratedRegex(@"(src|href)\s*=\s*""(https?:)?//", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ExternalUrl();
}
