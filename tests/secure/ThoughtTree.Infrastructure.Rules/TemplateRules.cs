using System.Text.Json.Nodes;

namespace ThoughtTree.Infrastructure.Rules;

/// <summary>ADR-046 template rules: compiles, no stored secrets, no telemetry or IP logging, environments differ only by parameters.</summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-046")]
public class TemplateRules
{
    private static IEnumerable<JsonNode> Resources() =>
        Bicep.Template["resources"] switch
        {
            JsonArray array => array.OfType<JsonNode>(),
            JsonObject symbolic => symbolic.Select(p => p.Value!),
            _ => [],
        };

    [Fact]
    public void Templates_and_parameter_files_compile()
    {
        Bicep.Template["resources"].ShouldNotBeNull();
        Bicep.Parameters.Keys.ShouldBe(Bicep.Environments, ignoreOrder: true);
    }

    [Fact]
    public void Only_static_web_apps_are_deployed()
    {
        Resources().Select(r => (string)r["type"]!).Distinct()
            .ShouldBe(["Microsoft.Web/staticSites"], "release 1 hosts the client on Static Web Apps only (ADR-046, ADR-074)");
    }

    [Fact]
    public void No_telemetry_service_and_no_logs_that_could_hold_ip_addresses()
    {
        var types = Resources().Select(r => ((string)r["type"]!).ToUpperInvariant()).ToList();

        types.ShouldNotContain(t => t.StartsWith("MICROSOFT.INSIGHTS/", StringComparison.Ordinal), "no Application Insights or diagnostic settings (ADR-046)");
        types.ShouldNotContain(t => t.StartsWith("MICROSOFT.OPERATIONALINSIGHTS/", StringComparison.Ordinal), "no Log Analytics workspace (ADR-046)");
        types.ShouldNotContain(t => t.EndsWith("/DIAGNOSTICSETTINGS", StringComparison.Ordinal));
        Bicep.Template.ToJsonString().ShouldNotContain("APPINSIGHTS", Case.Insensitive);
    }

    [Fact]
    public void Templates_hand_out_no_secrets()
    {
        var json = Bicep.Template.ToJsonString();
        json.ShouldNotContain("listSecrets", Case.Insensitive, "the deployment token is fetched at run time by the workflow, never output");
        json.ShouldNotContain("listKeys", Case.Insensitive);
        json.ShouldNotContain("repositoryToken", Case.Insensitive, "no repository link that stores a token in GitHub");

        var outputs = Bicep.Template["outputs"]?.AsObject() ?? [];
        foreach (var (name, output) in outputs)
        {
            ((string)output!["type"]!).ShouldNotStartWith("secure", Case.Insensitive);
            name.ShouldNotContain("key", Case.Insensitive);
            name.ShouldNotContain("token", Case.Insensitive);
            name.ShouldNotContain("secret", Case.Insensitive);
        }
    }

    [Fact]
    public void Sites_are_not_linked_to_the_repository()
    {
        foreach (var site in Resources())
        {
            ((string?)site["properties"]?["provider"]).ShouldBe("None");
            site["properties"]?["repositoryUrl"].ShouldBeNull();
        }
    }

    [Fact]
    public void Environments_set_the_same_parameters_and_nothing_else()
    {
        var names = Bicep.Parameters.ToDictionary(p => p.Key, p => p.Value["parameters"]!.AsObject().Select(x => x.Key).Order(StringComparer.Ordinal).ToList());
        names["production"].ShouldBe(names["test"], "test and production differ only in parameter values (ADR-046)");

        foreach (var environment in Bicep.Environments)
        {
            var lines = File.ReadAllLines(Repository.PathOf("infra", environment + ".bicepparam"))
                .Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal));
            lines.ShouldAllBe(l => l == "using 'main.bicep'" || l.StartsWith("param ", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("test", true)]
    [InlineData("production", false)]
    public void Only_the_test_site_hosts_previews(string environment, bool previews)
    {
        ((bool)Bicep.Parameters[environment]["parameters"]!["allowPreviews"]!["value"]!).ShouldBe(previews);
    }
}
