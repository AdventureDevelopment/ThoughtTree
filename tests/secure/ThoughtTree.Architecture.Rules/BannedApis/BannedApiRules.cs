using ThoughtTree.Architecture.Rules.Support;
using static ThoughtTree.Architecture.Rules.BannedApis.BannedApiAnalyzerRunner;

namespace ThoughtTree.Architecture.Rules.BannedApis;

/// <summary>
/// Proves that every entry of the banned API files fires as a compile error, and that the allowed
/// alternatives do not (ADR-034, ADR-039, ADR-027). Every new entry needs a snippet in <see cref="Snippets"/>.
/// The analyzer reports uses of a symbol (calls, member access, creation), not a bare declaration of a
/// variable or parameter of a banned type; for Domain the architecture rule
/// Domain_types_depend_on_nothing_outside_domain also covers declarations.
/// </summary>
[Trait("Intent", "Secure")]
public class BannedApiRules
{
    private const string Common = "BannedSymbols.txt";
    private const string Concurrency = "BannedSymbols.Concurrency.txt";
    private const string Domain = "BannedSymbols.Domain.txt";

    /// <summary>One snippet per banned symbol: code that uses exactly that symbol.</summary>
    private static readonly Dictionary<string, string> Snippets = new()
    {
        // ADR-034
        ["P:System.DateTime.Now"] = "_ = DateTime.Now;",
        ["P:System.DateTime.UtcNow"] = "_ = DateTime.UtcNow;",
        ["P:System.DateTime.Today"] = "_ = DateTime.Today;",
        ["P:System.DateTimeOffset.Now"] = "_ = DateTimeOffset.Now;",
        ["P:System.DateTimeOffset.UtcNow"] = "_ = DateTimeOffset.UtcNow;",
        ["T:System.Diagnostics.Stopwatch"] = "_ = System.Diagnostics.Stopwatch.StartNew();",
        ["P:System.Environment.TickCount"] = "_ = Environment.TickCount;",
        ["P:System.Environment.TickCount64"] = "_ = Environment.TickCount64;",
        ["M:System.Guid.NewGuid"] = "_ = Guid.NewGuid();",
        ["M:System.Guid.CreateVersion7"] = "_ = Guid.CreateVersion7();",
        ["M:System.Guid.CreateVersion7(System.DateTimeOffset)"] = "_ = Guid.CreateVersion7(default(DateTimeOffset));",
        ["M:System.Random.#ctor"] = "_ = new Random();",
        ["M:System.Random.#ctor(System.Int32)"] = "_ = new Random(42);",
        ["P:System.Random.Shared"] = "_ = Random.Shared.Next();",
        ["T:System.Security.Cryptography.RandomNumberGenerator"] = "_ = System.Security.Cryptography.RandomNumberGenerator.GetInt32(10);",

        // ADR-039
        ["T:System.Threading.Thread"] = "_ = Thread.CurrentThread;",
        ["M:System.Threading.Tasks.Task.Run(System.Action)"] = "_ = Task.Run(() => { });",
        ["M:System.Threading.Tasks.Task.Run(System.Action,System.Threading.CancellationToken)"] = "_ = Task.Run(() => { }, CancellationToken.None);",
        ["M:System.Threading.Tasks.Task.Run(System.Func{System.Threading.Tasks.Task})"] = "_ = Task.Run(() => Task.CompletedTask);",
        ["M:System.Threading.Tasks.Task.Run(System.Func{System.Threading.Tasks.Task},System.Threading.CancellationToken)"] = "_ = Task.Run(() => Task.CompletedTask, CancellationToken.None);",
        ["M:System.Threading.Tasks.Task.Run``1(System.Func{``0})"] = "_ = Task.Run(() => 1);",
        ["M:System.Threading.Tasks.Task.Run``1(System.Func{``0},System.Threading.CancellationToken)"] = "_ = Task.Run(() => 1, CancellationToken.None);",
        ["M:System.Threading.Tasks.Task.Run``1(System.Func{System.Threading.Tasks.Task{``0}})"] = "_ = Task.Run(() => Task.FromResult(1));",
        ["M:System.Threading.Tasks.Task.Run``1(System.Func{System.Threading.Tasks.Task{``0}},System.Threading.CancellationToken)"] = "_ = Task.Run(() => Task.FromResult(1), CancellationToken.None);",
        ["P:System.Threading.Tasks.Task.Factory"] = "_ = Task.Factory;",
        ["T:System.Threading.Tasks.TaskFactory"] = "_ = new TaskFactory();",
        ["T:System.Threading.Tasks.Parallel"] = "Parallel.For(0, 1, _ => { });",
        ["T:System.Threading.ThreadPool"] = "ThreadPool.QueueUserWorkItem(_ => { });",
        ["T:System.Threading.Timer"] = "using var timer = new System.Threading.Timer(_ => { });",
        ["T:System.Timers.Timer"] = "using var timer = new System.Timers.Timer();",
        ["M:System.Threading.Tasks.Task.Delay(System.Int32)"] = "_ = Task.Delay(1);",
        ["M:System.Threading.Tasks.Task.Delay(System.Int32,System.Threading.CancellationToken)"] = "_ = Task.Delay(1, CancellationToken.None);",
        ["M:System.Threading.Tasks.Task.Delay(System.TimeSpan)"] = "_ = Task.Delay(TimeSpan.Zero);",
        ["M:System.Threading.Tasks.Task.Delay(System.TimeSpan,System.Threading.CancellationToken)"] = "_ = Task.Delay(TimeSpan.Zero, CancellationToken.None);",
        ["M:System.Threading.Tasks.Task.WaitAsync(System.TimeSpan)"] = "_ = Task.CompletedTask.WaitAsync(TimeSpan.Zero);",
        ["M:System.Threading.Tasks.Task.WaitAsync(System.TimeSpan,System.Threading.CancellationToken)"] = "_ = Task.CompletedTask.WaitAsync(TimeSpan.Zero, CancellationToken.None);",
        ["M:System.Threading.CancellationTokenSource.#ctor(System.TimeSpan)"] = "using var source = new CancellationTokenSource(TimeSpan.Zero);",
        ["M:System.Threading.CancellationTokenSource.#ctor(System.Int32)"] = "using var source = new CancellationTokenSource(1);",

        // ADR-027
        ["N:System.IO"] = "using var stream = new System.IO.MemoryStream();",
        ["N:System.Net"] = "using var client = new System.Net.Http.HttpClient();",
        ["N:System.Runtime.InteropServices.JavaScript"] = "static void Use(System.Runtime.InteropServices.JavaScript.JSObject js) => _ = js.GetPropertyAsString(\"x\"); Use(null!);",
        ["N:Microsoft.JSInterop"] = "static void Use(Microsoft.JSInterop.IJSRuntime js) => _ = js.InvokeAsync<string>(\"x\", Array.Empty<object>()); Use(null!);",
        ["T:System.Console"] = "Console.WriteLine();",
        ["T:System.Environment"] = "_ = Environment.ProcessorCount;",
        ["T:System.Threading.Tasks.Task"] = "_ = Task.CompletedTask;",
        ["T:System.Threading.Tasks.Task`1"] = "static int Use(Task<int> task) => task.Result; _ = Use(null!);",
        ["T:System.Threading.Tasks.ValueTask"] = "_ = ValueTask.CompletedTask;",
        ["T:System.Threading.Tasks.ValueTask`1"] = "_ = new ValueTask<int>(1);",
        ["T:System.Collections.Generic.IAsyncEnumerable`1"] = "static void Use(IAsyncEnumerable<int> items) => _ = items.GetAsyncEnumerator(); Use(null!);",
    };

    [Theory]
    [Trait("ADR", "ADR-034")]
    [MemberData(nameof(EntriesOf), Common)]
    public Task Time_and_randomness_entry_is_a_compile_error(string symbol) => EntryFires(Common, symbol);

    [Theory]
    [Trait("ADR", "ADR-039")]
    [MemberData(nameof(EntriesOf), Concurrency)]
    public Task Concurrency_entry_is_a_compile_error(string symbol) => EntryFires(Concurrency, symbol);

    [Theory]
    [Trait("ADR", "ADR-027")]
    [MemberData(nameof(EntriesOf), Domain)]
    public Task Domain_entry_is_a_compile_error(string symbol) => EntryFires(Domain, symbol);

    [Fact]
    [Trait("ADR", "ADR-034")]
    [Trait("ADR", "ADR-039")]
    public async Task Time_provider_alternatives_stay_allowed()
    {
        const string body = """
            var time = TimeProvider.System;
            _ = time.GetUtcNow();
            _ = time.GetElapsedTime(time.GetTimestamp());
            using var timer = time.CreateTimer(_ => { }, null, TimeSpan.Zero, TimeSpan.Zero);
            using var source = new CancellationTokenSource(TimeSpan.Zero, time);
            _ = Task.Delay(TimeSpan.Zero, time);
            _ = Task.Delay(TimeSpan.Zero, time, CancellationToken.None);
            _ = Task.CompletedTask.WaitAsync(TimeSpan.Zero, time);
            _ = Task.FromResult(1);
            """;

        var diagnostics = await BannedApiDiagnosticsAsync(Wrap(body), FilesOf(Common, Concurrency));

        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [Trait("ADR", "ADR-034")]
    [Trait("ADR", "ADR-039")]
    [Trait("ADR", "ADR-027")]
    [InlineData("ThoughtTree.Domain", new[] { Common, Concurrency, Domain })]
    [InlineData("ThoughtTree.Contracts", new[] { Common })]
    [InlineData("ThoughtTree.Client.App", new[] { Common, Concurrency })]
    [InlineData("ThoughtTree.Client", new[] { Common })]
    [InlineData("ThoughtTree.Server.Core", new[] { Common, Concurrency })]
    [InlineData("ThoughtTree.Server", new[] { Common })]
    public async Task Each_project_gets_its_banned_symbol_files(string project, string[] expected)
    {
        var files = await MsBuild.ItemsAsync(Repository.PathOf("src", project, project + ".csproj"), "AdditionalFiles");

        files.Select(Path.GetFileName).Where(f => f!.StartsWith("BannedSymbols", StringComparison.Ordinal)).Order(StringComparer.Ordinal)
            .ShouldBe(expected.Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("ADR", "ADR-034")]
    public void Every_banned_entry_carries_a_reason()
    {
        foreach (var file in new[] { Common, Concurrency, Domain })
        {
            foreach (var line in Lines(file))
            {
                line.Split(';', 2).ElementAtOrDefault(1).ShouldNotBeNullOrWhiteSpace($"{file}: '{line}' needs a reason after ';'");
            }
        }
    }

    public static TheoryData<string> EntriesOf(string file) => [.. Lines(file).Select(l => l.Split(';')[0].Trim())];

    private static async Task EntryFires(string file, string symbol)
    {
        Snippets.TryGetValue(symbol, out var body)
            .ShouldBeTrue($"Add a snippet for '{symbol}' to BannedApiRules.Snippets that proves the entry fires.");

        var line = Lines(file).Single(l => l.Split(';')[0].Trim() == symbol);
        var onlyThisEntry = new BannedSymbolFile("BannedSymbols.txt", line);

        var withEntry = await BannedApiDiagnosticsAsync(Wrap(body!), [onlyThisEntry]);
        var withoutEntry = await BannedApiDiagnosticsAsync(Wrap(body!), []);

        withEntry.ShouldNotBeEmpty($"{file}: '{symbol}' did not fire on: {body}");
        withoutEntry.ShouldBeEmpty();
    }

    private static string Wrap(string body) => $$"""
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        public static class Snippet
        {
            public static void Run()
            {
                {{body}}
            }
        }
        """;

    private static IEnumerable<string> Lines(string file) => File.ReadAllLines(Repository.PathOf("src", file))
        .Select(l => l.Trim())
        .Where(l => l.Length > 0 && !l.StartsWith('#'));

    private static BannedSymbolFile[] FilesOf(params string[] files) =>
        [.. files.Select(f => BannedSymbolFile.FromDisk(Repository.PathOf("src", f)))];
}
