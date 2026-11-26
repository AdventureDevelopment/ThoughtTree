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
                projector = new Projector();
                foreach (var e in log.Events)
                {
                    projector.TryAdd(e);
                }

                held = projector.Tree;
                before = TreeText.Full(held);
            });

        "When the projector applies a rename, a move, a delete and an unknown change"
            .x(() =>
            {
                var more = new Log("b0000000-0000-0000-0000-000000000002")
                    .Rename(3, "a", "Renamed").Move(4, "b", null).Delete(5, "a").Rename(6, "missing", "x");
                foreach (var e in more.Events)
                {
                    projector.TryAdd(e);
                }
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
    public void Two_snapshots_of_one_projector_are_independent_versions()
    {
        Projector projector = default!;
        Snapshot first = default!;
        Snapshot second = default!;

        "Given a snapshot, then one more event, then a second snapshot"
            .x(() =>
            {
                projector = new Projector();
                projector.TryAdd(new Log().Create(1, "a", "A").Events[0]);
                first = projector.TakeSnapshot();
                projector.TryAdd(new Log("b0000000-0000-0000-0000-000000000002").Create(2, "b", "B").Events[0]);
                second = projector.TakeSnapshot();
            });

        "When each is resumed without further events"
            .x(() => { });

        "Then the first shows one item and the second shows two"
            .x(() =>
            {
                Projector.TryResume(first, [])!.Tree.ToOutline().ShouldBe("- A");
                Projector.TryResume(second, [])!.Tree.ToOutline().ShouldBe("- B\n- A");
            });
    }
}
