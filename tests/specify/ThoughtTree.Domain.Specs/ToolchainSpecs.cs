using LambdaTale;

namespace ThoughtTree.Domain.Specs;

/// <summary>Proves that the spec toolchain runs (xUnit v2 with LambdaTale 2.9.x, ADR-075).</summary>
[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-075")]
public class ToolchainSpecs
{
    [Scenario]
    public void Steps_of_a_scenario_run_in_the_order_written()
    {
        List<string> steps = default!;

        "Given a scenario that has run no step yet"
            .x(() => steps = []);

        "When its next step runs"
            .x(() => steps.Add("when"));

        "And one more step runs"
            .x(() => steps.Add("and"));

        "Then the steps ran in the order written"
            .x(() => steps.ShouldBe(["when", "and"]));
    }
}
