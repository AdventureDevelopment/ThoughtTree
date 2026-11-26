using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Harness;

/// <summary>Variables, parameters and lambda parameters have written-out names, never a single letter (AGENTS.md section 4b).</summary>
[Trait("Intent", "Secure")]
public sealed class NameRules
{
    private const string PendingList = "eng/pending-renames.txt";

    [Fact]
    public void No_variable_or_parameter_has_a_one_letter_name()
    {
        var pending = Pending().ToHashSet(StringComparer.Ordinal);
        var offenders = AllFiles()
            .Where(file => !pending.Contains(file))
            .SelectMany(file => OneLetterNamesIn(file, File.ReadAllText(Repository.PathOf(file))))
            .ToList();

        offenders.ShouldBeEmpty("write the name out (AGENTS.md section 4b)");
    }

    [Fact]
    public void Every_pending_file_still_has_a_one_letter_name()
    {
        var done = Pending()
            .Where(file => !File.Exists(Repository.PathOf(file)) || !OneLetterNamesIn(file, File.ReadAllText(Repository.PathOf(file))).Any())
            .ToList();

        done.ShouldBeEmpty($"remove renamed or missing files from {PendingList}");
    }

    [Theory]
    [InlineData("var x = 1;")]
    [InlineData("int Twice(int n) => n * 2;")]
    [InlineData("var all = list.Select(e => e.Id);")]
    [InlineData("var all = list.Select((e, index) => e.Id);")]
    [InlineData("for (var i = 0; i < 3; i++) { }")]
    [InlineData("foreach (var e in list) { }")]
    [InlineData("if (value is string s) { }")]
    [InlineData("if (value is { } v) { }")]
    [InlineData("try { } catch (Exception e) { }")]
    [InlineData("var (a, rest) = pair;")]
    [InlineData("int.TryParse(text, out var n);")]
    [InlineData("var all = from e in list select e;")]
    public void One_letter_names_are_found(string code)
    {
        OneLetterNamesIn("A.cs", code).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("var count = 1;")]
    [InlineData("var all = list.Select(itemEvent => itemEvent.Id);")]
    [InlineData("_ = list.Count;")]
    [InlineData("var all = list.Select(_ => 1);")]
    [InlineData("T First<T>(IEnumerable<T> items) => items.First();")]
    public void Written_out_names_discards_and_type_parameters_pass(string code)
    {
        OneLetterNamesIn("A.cs", code).ShouldBeEmpty();
    }

    private static IEnumerable<string> AllFiles() =>
        Repository.Files("src", "*.cs").Concat(Repository.Files("tests", "*.cs")).Select(Repository.Relative);

    private static IEnumerable<string> OneLetterNamesIn(string file, string code)
    {
        var root = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script)).GetRoot();
        return root.DescendantNodes()
            .Select(DeclaredName)
            .Where(name => name is { } token && token.ValueText.Length == 1 && token.ValueText != "_")
            .Select(name => $"{file}:{name!.Value.GetLocation().GetLineSpan().StartLinePosition.Line + 1} {name.Value.ValueText}");
    }

    private static SyntaxToken? DeclaredName(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax variable => variable.Identifier,
        ParameterSyntax parameter => parameter.Identifier,
        SingleVariableDesignationSyntax designation => designation.Identifier,
        ForEachStatementSyntax loop => loop.Identifier,
        CatchDeclarationSyntax caught => caught.Identifier,
        FromClauseSyntax from => from.Identifier,
        LetClauseSyntax let => let.Identifier,
        JoinClauseSyntax join => join.Identifier,
        QueryContinuationSyntax continuation => continuation.Identifier,
        _ => null,
    };

    private static IEnumerable<string> Pending() =>
        File.ReadAllLines(Repository.PathOf(PendingList))
            .Select(line => line.Split('#', 2)[0].Trim())
            .Where(line => line.Length > 0);
}
