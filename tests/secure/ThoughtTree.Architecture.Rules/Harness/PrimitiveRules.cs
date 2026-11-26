using System.Reflection;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Harness;

/// <summary>Public members of the Domain and the application layer take and return own types, not bare string, long or int (AGENTS.md section 4b).</summary>
[Trait("Intent", "Secure")]
public sealed class PrimitiveRules
{
    private const string ApprovedList = "eng/approved-primitives.txt";

    private static readonly Type[] Primitives = [typeof(string), typeof(long), typeof(int), typeof(long?), typeof(int?)];

    private static readonly string[] GeneratedMembers = ["ToString", "Equals", "GetHashCode", "Deconstruct", "PrintMembers", "<Clone>$"];

    private static readonly Assembly[] Checked =
    [
        typeof(ThoughtTree.Domain.Outline.Tree).Assembly,
        Assembly.Load("ThoughtTree.Client.App"),
    ];

    [Fact]
    public void Every_public_string_long_or_int_is_approved()
    {
        var approved = Approved().ToHashSet(StringComparer.Ordinal);
        var unapproved = Checked.SelectMany(a => a.GetExportedTypes()).SelectMany(PrimitivesIn).Where(p => !approved.Contains(p)).ToList();

        unapproved.ShouldBeEmpty($"give the value its own type, or list it in {ApprovedList} (AGENTS.md section 4b)");
    }

    [Fact]
    public void Every_approved_entry_still_exists()
    {
        var existing = Checked.SelectMany(a => a.GetExportedTypes()).SelectMany(PrimitivesIn).ToHashSet(StringComparer.Ordinal);

        Approved().Where(p => !existing.Contains(p)).ToList().ShouldBeEmpty($"remove stale lines from {ApprovedList}");
    }

    [Fact]
    public void Properties_parameters_and_results_are_found_and_single_value_types_are_allowed()
    {
        PrimitivesIn(typeof(SampleWithPrimitives)).ShouldBe(
        [
            $"{typeof(SampleWithPrimitives).FullName}.ctor(name)",
            $"{typeof(SampleWithPrimitives).FullName}.Count",
            $"{typeof(SampleWithPrimitives).FullName}.Find(text)",
            $"{typeof(SampleWithPrimitives).FullName}.Find:result",
        ], ignoreOrder: true);
        PrimitivesIn(typeof(SampleValue)).ShouldBeEmpty();
    }

    private static IEnumerable<string> PrimitivesIn(Type type)
    {
        if (type.IsEnum || IsSingleValueType(type))
        {
            yield break;
        }

        const BindingFlags declaredPublic = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var constructor in type.GetConstructors(declaredPublic))
        {
            foreach (var parameter in constructor.GetParameters().Where(p => Primitives.Contains(p.ParameterType)))
            {
                yield return $"{type.FullName}.ctor({parameter.Name})";
            }
        }

        foreach (var property in type.GetProperties(declaredPublic).Where(p => Primitives.Contains(p.PropertyType)))
        {
            yield return $"{type.FullName}.{property.Name}";
        }

        foreach (var method in type.GetMethods(declaredPublic).Where(m => !m.IsSpecialName && !GeneratedMembers.Contains(m.Name)))
        {
            foreach (var parameter in method.GetParameters().Where(p => Primitives.Contains(p.ParameterType)))
            {
                yield return $"{type.FullName}.{method.Name}({parameter.Name})";
            }

            if (Primitives.Contains(method.ReturnType))
            {
                yield return $"{type.FullName}.{method.Name}:result";
            }
        }
    }

    private static bool IsSingleValueType(Type type) =>
        type.IsValueType && type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length == 1;

    private static IEnumerable<string> Approved() =>
        File.ReadAllLines(Repository.PathOf(ApprovedList))
            .Select(l => l.Split('#', 2)[0].Trim())
            .Where(l => l.Length > 0);

    public sealed class SampleWithPrimitives(string name)
    {
        public int Count => name.Length;

        public string Find(string text) => name + text;

        public SampleValue Wrap() => new(name);
    }

    public readonly record struct SampleValue(string Value);
}
