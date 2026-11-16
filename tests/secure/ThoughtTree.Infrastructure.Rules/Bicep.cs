using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;

namespace ThoughtTree.Infrastructure.Rules;

/// <summary>Compiles the templates in infra/ with the Bicep CLI from NuGet; no Azure login, no network.</summary>
internal static class Bicep
{
    private static readonly string Executable = typeof(Bicep).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "BicepPath").Value!;

    private static readonly Lazy<Compiled> Output = new(Compile);

    public static JsonNode Template => Output.Value.Template;

    public static IReadOnlyDictionary<string, JsonNode> Parameters => Output.Value.Parameters;

    public static readonly IReadOnlyList<string> Environments = ["test", "production"];

    private static Compiled Compile()
    {
        if (!OperatingSystem.IsWindows())
        {
            // NuGet does not keep the executable bit.
            File.SetUnixFileMode(Executable, File.GetUnixFileMode(Executable) | UnixFileMode.UserExecute);
        }

        var outDir = Path.Combine(Path.GetTempPath(), "thoughttree-bicep-" + Environment.ProcessId);
        Directory.CreateDirectory(outDir);

        var templatePath = Path.Combine(outDir, "main.json");
        Run("build", Repository.PathOf("infra", "main.bicep"), "--outfile", templatePath);

        var parameters = new Dictionary<string, JsonNode>();
        foreach (var environment in Environments)
        {
            var path = Path.Combine(outDir, environment + ".parameters.json");
            Run("build-params", Repository.PathOf("infra", environment + ".bicepparam"), "--outfile", path);
            parameters[environment] = JsonNode.Parse(File.ReadAllText(path))!;
        }

        return new Compiled(JsonNode.Parse(File.ReadAllText(templatePath))!, parameters);
    }

    private static void Run(params string[] arguments)
    {
        var start = new ProcessStartInfo(Executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Repository.Root,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("bicep did not start");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, $"bicep {string.Join(' ', arguments)} failed: {stderr}{stdout.GetAwaiter().GetResult()}");
    }

    private sealed record Compiled(JsonNode Template, IReadOnlyDictionary<string, JsonNode> Parameters);
}
