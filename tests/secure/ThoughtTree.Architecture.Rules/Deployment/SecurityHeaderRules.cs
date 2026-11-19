using System.Text.Json;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Deployment;

/// <summary>
/// ADR-045: the hosted app is served with a strict Content Security Policy and Referrer-Policy: no-referrer, sent
/// as HTTP headers from the Azure Static Web Apps configuration file. Every source is the app's own origin;
/// scripts allow only 'wasm-unsafe-eval' (Blazor WebAssembly) and the hash of the build's inline import map,
/// filled in at publish time (eng/deploy/publish-client.sh). The deployed headers are checked after each
/// upload by eng/deploy/check-headers.sh. Headers come only from globalHeaders: no route may set or override one,
/// and no second spelling of a header may exist, so the policy read here is the policy served.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-045")]
public class SecurityHeaderRules
{
    private const string ImportMapPlaceholder = "'{importmap-hash}'";

    private static readonly string[] SelfOnly = ["default-src", "style-src", "img-src", "font-src", "connect-src", "manifest-src", "worker-src", "media-src", "base-uri", "form-action"];

    private static readonly string[] Nothing = ["object-src", "frame-ancestors"];

    private static readonly HashSet<string> KnownSections = new(StringComparer.Ordinal) { "$comment", "globalHeaders", "navigationFallback", "mimeTypes", "routes" };

    private static readonly HashSet<string> KnownRouteProperties = new(StringComparer.Ordinal) { "route", "statusCode" };

    private static string ConfigText => File.ReadAllText(Repository.PathOf("src", "ThoughtTree.Client", "wwwroot", "staticwebapp.config.json"));

    [Fact]
    public void Every_source_is_the_own_origin()
    {
        var policy = Policy();

        foreach (var directive in SelfOnly)
        {
            policy.ShouldContainKey(directive);
            policy[directive].ShouldBe(["'self'"], $"{directive}: own origin only (ADR-045)");
        }

        foreach (var directive in Nothing)
        {
            policy.ShouldContainKey(directive);
            policy[directive].ShouldBe(["'none'"]);
        }
    }

    [Fact]
    public void Scripts_allow_only_the_own_origin_webassembly_and_the_import_map_hash()
    {
        var policy = Policy();

        policy.ShouldContainKey("script-src");
        policy["script-src"].ShouldBe(["'self'", "'wasm-unsafe-eval'", ImportMapPlaceholder]);
        policy.Values.SelectMany(v => v).ShouldNotContain("'unsafe-inline'");
        policy.Values.SelectMany(v => v).ShouldNotContain("'unsafe-eval'");
    }

    [Fact]
    public void No_directive_names_another_origin()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "'self'", "'none'", "'wasm-unsafe-eval'", ImportMapPlaceholder };

        Policy().SelectMany(d => d.Value.Where(s => !allowed.Contains(s)).Select(s => $"{d.Key} {s}"))
            .ShouldBeEmpty("ADR-045: nothing from third parties");
    }

    [Fact]
    public void The_referrer_is_never_sent()
    {
        Headers()["Referrer-Policy"].ShouldBe("no-referrer");
    }

    [Fact]
    public void Headers_come_only_from_global_headers()
    {
        ConfigProblems(ConfigText).ShouldBeEmpty("no route, override or second spelling may change the headers checked here");
    }

    [Fact]
    public void The_built_in_sign_in_providers_are_switched_off()
    {
        using var document = JsonDocument.Parse(ConfigText);
        var blocked = document.RootElement.GetProperty("routes").EnumerateArray()
            .Where(r => r.TryGetProperty("statusCode", out var code) && code.GetInt32() == 404)
            .Select(r => r.GetProperty("route").GetString())
            .ToList();

        blocked.ShouldContain("/.auth/login/github");
        blocked.ShouldContain("/.auth/login/aad");
    }

    [Theory]
    [InlineData("""{"globalHeaders": {"Referrer-Policy": "no-referrer"}, "routes": [{"route": "/*", "headers": {"Content-Security-Policy": "default-src *"}}]}""")]
    [InlineData("""{"globalHeaders": {"Referrer-Policy": "no-referrer", "referrer-policy": "unsafe-url"}}""")]
    [InlineData("""{"globalHeaders": {"Referrer-Policy": "no-referrer"}, "globalHeaders": {}}""")]
    [InlineData("""{"globalHeaders": {"Referrer-Policy": "no-referrer"}, "responseOverrides": {"404": {"rewrite": "/x"}}}""")]
    [InlineData("""{"globalHeaders": {"Referrer-Policy": "no-referrer"}, "routes": [{"route": "/x", "rewrite": "/y"}]}""")]
    public void A_header_override_is_found(string json)
    {
        ConfigProblems(json).ShouldNotBeEmpty();
    }

    internal static IEnumerable<string> ConfigProblems(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var problems = new List<string>();

        problems.AddRange(Duplicates(root, string.Empty));
        problems.AddRange(root.EnumerateObject().Where(p => !KnownSections.Contains(p.Name)).Select(p => $"unknown section {p.Name}"));
        if (root.TryGetProperty("globalHeaders", out var headers))
        {
            problems.AddRange(Duplicates(headers, "globalHeaders", StringComparer.OrdinalIgnoreCase));
        }

        if (root.TryGetProperty("routes", out var routes))
        {
            foreach (var route in routes.EnumerateArray())
            {
                problems.AddRange(route.EnumerateObject().Where(p => !KnownRouteProperties.Contains(p.Name)).Select(p => $"route {route.GetProperty("route")}: {p.Name}"));
            }
        }

        return problems;
    }

    private static IEnumerable<string> Duplicates(JsonElement element, string where, StringComparer? comparer = null) =>
        element.EnumerateObject().GroupBy(p => p.Name, comparer ?? StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => $"{where}: {g.Key} appears {g.Count()} times");

    private static Dictionary<string, string> Headers()
    {
        ConfigProblems(ConfigText).ShouldBeEmpty();
        using var document = JsonDocument.Parse(ConfigText);
        return document.RootElement.GetProperty("globalHeaders").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
    }

    private static Dictionary<string, string[]> Policy() =>
        Headers()["Content-Security-Policy"]
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(p => p[0], p => p[1..], StringComparer.Ordinal);
}
