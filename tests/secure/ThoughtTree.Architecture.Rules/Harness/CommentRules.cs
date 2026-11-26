using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Harness;

/// <summary>Every inline comment in src and tests is approved in eng/approved-comments.txt (AGENTS.md section 4b).</summary>
[Trait("Intent", "Secure")]
public sealed partial class CommentRules
{
    private const string ApprovedList = "eng/approved-comments.txt";
    private const string ReferenceModel = "tests/secure/ThoughtTree.Domain.Model/ReferenceModel.cs";

    [Fact]
    public void Every_inline_comment_is_approved()
    {
        var approved = Approved().ToHashSet();
        var unapproved = AllComments()
            .Where(comment => !approved.Contains(comment) && !IsReferenceModelRuleMarker(comment))
            .Select(comment => $"{comment.File} :: {comment.Text}")
            .ToList();

        unapproved.ShouldBeEmpty($"express it in code, or list it in {ApprovedList} (AGENTS.md section 4b)");
    }

    [Fact]
    public void Every_approved_comment_still_exists()
    {
        var existing = AllComments().ToHashSet();
        var stale = Approved().Where(comment => !existing.Contains(comment)).Select(comment => $"{comment.File} :: {comment.Text}").ToList();

        stale.ShouldBeEmpty($"remove stale lines from {ApprovedList}");
    }

    [Theory]
    [InlineData("var x = 1; // " + "a comment", 1)]
    [InlineData("/* " + "a block */ var x = 1;", 1)]
    [InlineData("/// <summary>A doc comment.</summary>\nclass A { }", 0)]
    [InlineData("var url = \"https:" + "//example.org\";", 0)]
    public void Inline_comments_are_found_and_doc_comments_and_strings_are_not(string code, int expected)
    {
        CommentsIn("A.cs", code).Count().ShouldBe(expected);
    }

    [Theory]
    [InlineData(ReferenceModel, "// R5", true)]
    [InlineData(ReferenceModel, "// R18, R19: the sort is stable", true)]
    [InlineData(ReferenceModel, "// explains the code", false)]
    [InlineData("src/A.cs", "// R5", false)]
    public void Only_the_reference_model_may_carry_rule_markers(string file, string text, bool allowed)
    {
        IsReferenceModelRuleMarker(new Comment(file, text)).ShouldBe(allowed);
    }

    private static IEnumerable<Comment> AllComments() =>
        Repository.Files("src", "*.cs").Concat(Repository.Files("tests", "*.cs"))
            .SelectMany(file => CommentsIn(Repository.Relative(file), File.ReadAllText(file)));

    private static IEnumerable<Comment> CommentsIn(string file, string code) =>
        CSharpSyntaxTree.ParseText(code).GetRoot().DescendantTrivia()
            .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            .Select(trivia => new Comment(file, trivia.ToString().Trim()));

    private static IEnumerable<Comment> Approved() =>
        File.ReadAllLines(Repository.PathOf(ApprovedList))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(" :: ", 2))
            .Select(parts => new Comment(parts[0].Trim(), parts[1].Trim()));

    private static bool IsReferenceModelRuleMarker(Comment comment) =>
        comment.File == ReferenceModel && RuleMarker().IsMatch(comment.Text);

    [GeneratedRegex(@"^// R\d+(, R\d+)*(:|$)")]
    private static partial Regex RuleMarker();

    private sealed record Comment(string File, string Text);
}
