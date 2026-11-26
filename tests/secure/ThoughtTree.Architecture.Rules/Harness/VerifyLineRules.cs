using System.Diagnostics;
using System.Globalization;
using System.Text;
using ThoughtTree.Architecture.Rules.Support;
using Xunit.Abstractions;

namespace ThoughtTree.Architecture.Rules.Harness;

/// <summary>
/// The verify lines of docs/adr (docs/adr/README.md, Conventions). Static lines (grep, sha256sum) run here
/// against a clean copy of the working tree. For <c>dotnet test --filter "ADR=ADR-0xx"</c> lines this class
/// only reports which ones match no test yet; it does not fail for them while the code they verify does not
/// exist (foundation stage 1). The report goes to the test output and to verify-report.md next to the test assembly.
/// </summary>
[Trait("Intent", "Secure")]
public sealed class VerifyLineRules(ITestOutputHelper output)
{
    [Fact]
    public void Verify_blocks_can_be_read()
    {
        AdrVerifyLines.All.Count.ShouldBeGreaterThan(0);
        AdrVerifyLines.All.Where(l => l.FilterAdr is not null).ShouldAllBe(l => l.FilterAdr == l.Adr, "a verify filter names its own ADR");
    }

    [Theory]
    [MemberData(nameof(StaticLines))]
    public void Static_verify_line_holds(string adr, string command, string expected)
    {
        var (exitCode, stdout, stderr) = CleanCopy.Run(command);

        if (expected.Length > 0)
        {
            var actual = stdout.Trim();
            (actual == expected || actual.StartsWith(expected + " ", StringComparison.Ordinal))
                .ShouldBeTrue($"{adr}: '{command}' printed '{actual}', expected '{expected}'. {stderr}");
        }
        else
        {
            exitCode.ShouldBe(0, $"{adr}: '{command}' failed. {stdout}{stderr}");
        }
    }

    [Fact]
    public void Report_verify_filters_without_tests()
    {
        var tagged = Repository.Files("tests", "*.cs")
            .SelectMany(f => TestPlacementRules.AdrTrait().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet();

        var filters = AdrVerifyLines.All.Where(l => l.FilterAdr is not null).ToList();
        var matched = filters.Where(l => tagged.Contains(l.FilterAdr!)).ToList();
        var deferred = filters.Where(l => !tagged.Contains(l.FilterAdr!) && l.When is not null).ToList();
        var missing = filters.Where(l => !tagged.Contains(l.FilterAdr!) && l.When is null).ToList();
        var irregular = AdrVerifyLines.All.Where(l => l.When is not null && !l.HasCanonicalWhen).ToList();

        var report = new StringBuilder()
            .AppendLine("# Verify filters and tests")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"`dotnet test --filter \"ADR=...\"` lines: {filters.Count}; matched by at least one test: {matched.Count}; "
                + $"no test yet: {missing.Count}; no test yet, deferred by a `when:` marker: {deferred.Count}.")
            .AppendLine()
            .AppendLine("Matched: " + Ids(matched))
            .AppendLine()
            .AppendLine("No test yet: " + Ids(missing))
            .AppendLine()
            .AppendLine("Deferred: " + string.Join(", ", deferred.Select(l => $"{l.Adr} ({l.When})")))
            .AppendLine()
            .AppendLine("Markers not in the form `# when: <loop>` (recognised anyway):");
        foreach (var line in irregular)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"- {line.Adr}: `# {line.Comment}`");
        }

        var text = report.ToString();
        output.WriteLine(text);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "verify-report.md"), text);

        (matched.Count + missing.Count + deferred.Count).ShouldBe(filters.Count);
    }

    [Theory]
    [InlineData("grep -rE 'a|b' src | wc -l   # expect 0", "grep -rE 'a|b' src | wc -l", "expect 0")]
    [InlineData("grep -rnE '#[0-9a-f]{3}' src | wc -l   # expect 0: no colours", "grep -rnE '#[0-9a-f]{3}' src | wc -l", "expect 0: no colours")]
    [InlineData("dotnet test --filter \"ADR=ADR-021\"   # when: server", "dotnet test --filter \"ADR=ADR-021\"", "when: server")]
    [InlineData("sha256sum LICENSE", "sha256sum LICENSE", "")]
    public void Comments_are_split_outside_quotes(string line, string command, string comment)
    {
        AdrVerifyLines.SplitComment(line).ShouldBe((command, comment));
    }

    public static TheoryData<string, string, string> StaticLines()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var line in AdrVerifyLines.All.Where(l => l.IsStatic))
        {
            data.Add(line.Adr, line.Command, line.Command.StartsWith("! ", StringComparison.Ordinal) ? string.Empty : line.Expected ?? string.Empty);
        }

        return data;
    }

    private static string Ids(IEnumerable<VerifyLine> lines) => string.Join(", ", lines.Select(l => l.Adr));

    /// <summary>
    /// A copy of the tracked and untracked, not ignored files, so that build output (bin/, obj/) cannot
    /// produce matches; created once per test run in the temp folder.
    /// </summary>
    private static class CleanCopy
    {
        private static readonly Lazy<string> Directory = new(Create);

        public static (int ExitCode, string Stdout, string Stderr) Run(string command) =>
            Execute("bash", Directory.Value, "-c", command);

        private static string Create()
        {
            var target = Path.Combine(Path.GetTempPath(), "thoughttree-verify-" + Environment.ProcessId);
            if (System.IO.Directory.Exists(target))
            {
                System.IO.Directory.Delete(target, recursive: true);
            }

            var (exitCode, files, error) = Execute("git", Repository.Root, "ls-files", "--cached", "--others", "--exclude-standard");
            exitCode.ShouldBe(0, error);
            foreach (var file in files.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var source = Repository.PathOf(file);
                var deletedInTheWorkingTree = !File.Exists(source);
                if (deletedInTheWorkingTree)
                {
                    continue;
                }

                var destination = Path.Combine(target, file);
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination);
            }

            return target;
        }

        private static (int ExitCode, string Stdout, string Stderr) Execute(string program, string workingDirectory, params string[] arguments)
        {
            var start = new ProcessStartInfo(program)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start) ?? throw new InvalidOperationException(program + " did not start");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr);
        }
    }
}
