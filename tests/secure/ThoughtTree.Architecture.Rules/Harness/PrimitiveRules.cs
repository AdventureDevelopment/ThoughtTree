using System.Reflection;
using ThoughtTree.Architecture.Rules.Support;

namespace ThoughtTree.Architecture.Rules.Harness;

/// <summary>Public members of the Domain and the application layer take and return own types, not bare string, long or int, and hold or take states, not bool (AGENTS.md section 4b).</summary>
[Trait("Intent", "Secure")]
public sealed class PrimitiveRules
{
    private const string ApprovedList = "eng/approved-primitives.txt";

    private static readonly Type[] Primitives = [typeof(string), typeof(long), typeof(int), typeof(long?), typeof(int?)];

    private static readonly Type[] Flags = [typeof(bool), typeof(bool?)];

    private static readonly string[] GeneratedMembers = ["ToString", "Equals", "GetHashCode", "Deconstruct", "PrintMembers", "<Clone>$"];

    private static readonly Assembly[] Checked =
    [
        Assembly.Load("ThoughtTree.Domain"),
        Assembly.Load("ThoughtTree.Client.App"),
    ];

    [Fact]
    public void Every_public_primitive_or_stored_flag_is_approved()
    {
        var approved = Approved().ToHashSet(StringComparer.Ordinal);
        var unapproved = Checked.SelectMany(assembly => assembly.GetExportedTypes()).SelectMany(PrimitivesIn).Where(primitive => !approved.Contains(primitive)).ToList();

        unapproved.ShouldBeEmpty($"give the value its own type, or list it in {ApprovedList} (AGENTS.md section 4b)");
    }

    [Fact]
    public void Every_approved_entry_still_exists()
    {
        var existing = Checked.SelectMany(assembly => assembly.GetExportedTypes()).SelectMany(PrimitivesIn).ToHashSet(StringComparer.Ordinal);

        Approved().Where(entry => !existing.Contains(entry)).ToList().ShouldBeEmpty($"remove stale lines from {ApprovedList}");
    }

    [Fact]
    public void Properties_parameters_and_results_are_found_and_single_value_types_and_bool_questions_are_allowed()
    {
        PrimitivesIn(typeof(SampleWithPrimitives)).ShouldBe(
        [
            $"{typeof(SampleWithPrimitives).FullName}.ctor(name)",
            $"{typeof(SampleWithPrimitives).FullName}.Count",
            $"{typeof(SampleWithPrimitives).FullName}.Find(text)",
            $"{typeof(SampleWithPrimitives).FullName}.Find:result",
            $"{typeof(SampleWithPrimitives).FullName}.Rows:result",
            $"{typeof(SampleWithPrimitives).FullName}.Sizes",
            $"{typeof(SampleWithPrimitives).FullName}.Ready",
            $"{typeof(SampleWithPrimitives).FullName}.Check(strict)",
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
            foreach (var parameter in constructor.GetParameters().Where(candidate => HoldsPrimitiveOrFlag(candidate.ParameterType)))
            {
                yield return $"{type.FullName}.ctor({parameter.Name})";
            }
        }

        foreach (var property in type.GetProperties(declaredPublic).Where(candidate => HoldsPrimitiveOrFlag(candidate.PropertyType)))
        {
            yield return $"{type.FullName}.{property.Name}";
        }

        foreach (var method in type.GetMethods(declaredPublic).Where(candidate => !candidate.IsSpecialName && !GeneratedMembers.Contains(candidate.Name)))
        {
            foreach (var parameter in method.GetParameters().Where(candidate => HoldsPrimitiveOrFlag(candidate.ParameterType)))
            {
                yield return $"{type.FullName}.{method.Name}({parameter.Name})";
            }

            if (HoldsPrimitive(method.ReturnType))
            {
                yield return $"{type.FullName}.{method.Name}:result";
            }
        }
    }

    private static bool HoldsPrimitiveOrFlag(Type type) => HoldsPrimitive(type) || Holds(type, Flags);

    private static bool HoldsPrimitive(Type type) => Holds(type, Primitives);

    private static bool Holds(Type type, Type[] kinds) =>
        kinds.Contains(type)
        || (type.IsArray && Holds(type.GetElementType()!, kinds))
        || (type.IsGenericType && type.GetGenericArguments().Any(argument => Holds(argument, kinds)));

    private static bool IsSingleValueType(Type type) =>
        type.IsValueType && type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length == 1;

    private static IEnumerable<string> Approved() =>
        File.ReadAllLines(Repository.PathOf(ApprovedList))
            .Select(line => line.Split('#', 2)[0].Trim())
            .Where(line => line.Length > 0);

    public sealed class SampleWithPrimitives(string name)
    {
        public int Count => name.Length;

        public string Find(string text) => name + text;

        public SampleValue Wrap() => new(name);

        public IReadOnlyList<int> Sizes => [name.Length];

        public bool Ready { get; } = name.Length > 0;

        public SampleValue Check(bool strict) => new(strict ? name : "");

        public bool IsEmpty() => name.Length == 0;

        public IReadOnlyList<(SampleValue Value, long Position)> Rows() => [(new SampleValue(name), 0)];
    }

    public readonly record struct SampleValue(string Value);
}
