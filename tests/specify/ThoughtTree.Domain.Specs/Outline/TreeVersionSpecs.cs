using LambdaTale;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-002")]
public sealed class TreeVersionSpecs
{
    [Scenario]
    public void A_tree_a_caller_holds_does_not_change_when_the_projector_goes_on()
    {
        Projector projector = default!;
        Tree held = default!;
        var before = "";

        "Given a projector with two items and a tree the caller keeps"
            .x(() =>
            {
                var log = new Log().Create(1, "a", "A").Create(2, "b", "B", parent: "a");
                projector = log.Events.Aggregate(Projector.Empty, (p, e) => p.Add(e));
                held = projector.Tree;
                before = TreeText.Full(held);
            });

        "When the projector applies a rename, a move, a delete and an unknown change"
            .x(() =>
            {
                var more = new Log("b0000000-0000-0000-0000-000000000002")
                    .Rename(3, "a", "Renamed").Move(4, "b", null).Delete(5, "a").Rename(6, "missing", "x");
                projector = more.Events.Aggregate(projector, (p, e) => p.Add(e));
            });

        "Then the kept tree still shows the earlier state, and the projector's tree shows the new one"
            .x(() =>
            {
                TreeText.Full(held).ShouldBe(before);
                held.Anomalies.ShouldBeEmpty();
                TreeText.Full(projector.Tree).ShouldNotBe(before);
                projector.Tree.Anomalies.Count.ShouldBe(1);
            });
    }

    [Scenario]
    public void Two_kept_projectors_of_one_history_are_independent_versions()
    {
        Projector first = default!;
        Projector second = default!;

        "Given a projector kept after one event"
            .x(() => first = Projector.Empty.Add(new Log().Create(1, "a", "A").Events[0]));

        "When the app keeps a second one after one more event"
            .x(() => second = first.Add(new Log("b0000000-0000-0000-0000-000000000002").Create(2, "b", "B").Events[0]));

        "Then the first shows one item and the second shows two"
            .x(() =>
            {
                first.Tree.ToOutline().ShouldBe("- A");
                second.Tree.ToOutline().ShouldBe("- B\n- A");
            });
    }
}
