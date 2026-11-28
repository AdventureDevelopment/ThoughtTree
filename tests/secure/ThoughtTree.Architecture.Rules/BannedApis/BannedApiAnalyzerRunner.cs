using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ThoughtTree.Architecture.Rules.BannedApis;

/// <summary>
/// Compiles a code snippet in memory and runs the real banned API analyzer (the package the build uses,
/// copied next to the tests) with the given banned symbol files.
/// </summary>
internal static class BannedApiAnalyzerRunner
{
    public const string BannedApiDiagnosticId = "RS0030";

    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = LoadAnalyzers();

    private static readonly ImmutableArray<MetadataReference> References = LoadReferences();

    public static async Task<ImmutableArray<Diagnostic>> BannedApiDiagnosticsAsync(string code, IEnumerable<AdditionalText> bannedSymbolFiles)
    {
        var compilation = CSharpCompilation.Create(
            "Snippet",
            [CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest))],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var compileErrors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        compileErrors.ShouldBeEmpty("the snippet must compile:\n" + code);

        var diagnostics = await compilation
            .WithAnalyzers(Analyzers, new AnalyzerOptions([.. bannedSymbolFiles]))
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);

        return [.. diagnostics.Where(diagnostic => diagnostic.Id == BannedApiDiagnosticId)];
    }

    private static ImmutableArray<DiagnosticAnalyzer> LoadAnalyzers()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "BannedApiAnalyzer");
        var analyzers = Directory.GetFiles(directory, "*.dll")
            .Select(Assembly.LoadFrom)
            .SelectMany(TypesLoadableWithoutRoslynWorkspaces)
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttributes<DiagnosticAnalyzerAttribute>().Any(attribute => attribute.Languages.Contains(LanguageNames.CSharp)))
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .ToImmutableArray();

        analyzers.ShouldNotBeEmpty("the banned API analyzer must be copied to " + directory);
        return analyzers;
    }

    private static IEnumerable<Type> TypesLoadableWithoutRoslynWorkspaces(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        platform.ShouldContain(assemblyPath => Path.GetFileName(assemblyPath) == "Microsoft.JSInterop.dll");
        return [.. platform.Select(assemblyPath => MetadataReference.CreateFromFile(assemblyPath))];
    }
}

/// <summary>A banned symbol file for the analyzer, read from disk or given as text.</summary>
internal sealed class BannedSymbolFile(string path, string text) : AdditionalText
{
    public override string Path { get; } = path;

    public static BannedSymbolFile FromDisk(string path) => new(path, File.ReadAllText(path));

    public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
}
