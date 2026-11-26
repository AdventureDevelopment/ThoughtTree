using System.Diagnostics;
using System.Text.Json;

namespace ThoughtTree.Architecture.Rules.Support;

/// <summary>Evaluates a project with the SDK that runs the tests, without building it.</summary>
internal static class MsBuild
{
    public static async Task<IReadOnlyList<string>> ItemsAsync(string projectPath, string itemType)
    {
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var start = new ProcessStartInfo(dotnet)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Repository.Root,
        };
        foreach (var argument in new[] { "msbuild", projectPath, "-nologo", "-getItem:" + itemType })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start");
        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        process.ExitCode.ShouldBe(0, error + output);

        using var json = JsonDocument.Parse(output);
        return [.. json.RootElement.GetProperty("Items").GetProperty(itemType).EnumerateArray()
            .Select(item => item.GetProperty("FullPath").GetString()!)];
    }
}
