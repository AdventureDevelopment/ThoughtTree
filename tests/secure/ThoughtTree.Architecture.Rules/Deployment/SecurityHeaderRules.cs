using System.Text.Json;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Deployment;

/// <summary>
/// ADR-045: the hosted app is served with a strict Content Security Policy and Referrer-Policy: no-referrer, sent
/// as HTTP headers from the Azure Static Web Apps configuration file. Every source is the app's own origin;
/// scripts allow only 'wasm-unsafe-eval' (Blazor WebAssembly) and the hash of the build's inline import map,
/// filled in at publish time (eng/deploy/publish-client.sh). The deployed headers are checked after each
/// upload by eng/deploy/upload-site.sh.
/// </summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-045")]
public class SecurityHeaderRules
{
    private const string ImportMapPlaceholder = "'{importmap-hash}'";

    private static readonly string[] SelfOnly = ["default-src", "style-src", "img-src", "font-src", "connect-src", "manifest-src", "worker-src", "media-src", "base-uri", "form-action"];

    private static readonly string[] Nothing = ["object-src", "frame-ancestors"];

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

    private static Dictionary<string, string> Headers()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Repository.PathOf("src", "ThoughtTree.Client", "wwwroot", "staticwebapp.config.json")));
        return document.RootElement.GetProperty("globalHeaders").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
    }

    private static Dictionary<string, string[]> Policy() =>
        Headers()["Content-Security-Policy"]
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(p => p[0], p => p[1..], StringComparer.Ordinal);
}
