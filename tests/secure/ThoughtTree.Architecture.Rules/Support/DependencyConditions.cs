using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent.Conditions;

namespace ThoughtTree.Architecture.Rules.Support;

/// <summary>
/// Conditions on the dependency targets themselves. ArchUnitNET's Types() only covers the loaded source
/// assemblies, so a rule like NotDependOnAny(Types().That()...) is silently empty for framework types
/// (JSInterop, Thread, Task.Run); these conditions match the full names of whatever a type uses.
/// </summary>
internal static class DependencyConditions
{
    /// <summary>The type uses no type whose full name matches <paramref name="pattern"/>.</summary>
    public static Func<IType, ConditionResult> UsesNoTypeMatching(string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        return type =>
        {
            var offending = type.Dependencies.Select(d => d.Target.FullName).Where(n => regex.IsMatch(n)).Distinct().ToList();
            return new ConditionResult(type, offending.Count == 0, "uses " + string.Join(", ", offending));
        };
    }

    /// <summary>The type calls no method whose full name matches <paramref name="pattern"/>.</summary>
    public static Func<IType, ConditionResult> CallsNoMethodMatching(string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        return type =>
        {
            var offending = type.Dependencies.OfType<MethodCallDependency>()
                .Select(d => d.TargetMember.FullName).Where(n => regex.IsMatch(n)).Distinct().ToList();
            return new ConditionResult(type, offending.Count == 0, "calls " + string.Join(", ", offending));
        };
    }
}
