using LambdaTale;
using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

/// <summary>Events the projection cannot carry out as written, and their reports (ADR-004, ADR-016, ADR-023).</summary>
[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-004")]
[Trait("ADR", "ADR-016")]
[Trait("ADR", "ADR-023")]
public sealed class UnappliedEventSpecs
{
    private static IEnumerable<(AnomalyKind Kind, ItemId? Item)> Reports(Tree tree) =>
        tree.Anomalies.Select(a => (a.Kind, a.Item));

    [Scenario]
    public void ADR_023_A_move_into_the_items_own_subtree_is_not_applied_and_is_reported()
    {
        Tree tree = default!;

        "Given 'Parent' with child 'Child' under 'Root'"
            .x(() => { });

        "When the tree is rebuilt from a log that then moves 'Parent' under 'Child'"
            .x(() => tree = Projection.Replay(new Log().Create(1, "root", "Root")
                .Create(2, "parent", "Parent", parent: "root").Create(3, "child", "Child", parent: "parent")
                .Move(4, "parent", "child").Events));

        "Then 'Parent' stays under 'Root' with its child"
            .x(() => tree.ToOutline().ShouldBe("- Root\n  - Parent\n    - Child"));

        "And the move is reported for 'Parent'"
            .x(() => Reports(tree).ShouldBe([(AnomalyKind.Cycle, Log.Id("parent"))]));
    }

    [Scenario]
    public void ADR_004_A_move_to_a_parent_that_does_not_exist_is_not_applied_and_is_reported()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that moves 'A' under an item that was never created"
            .x(() => tree = Projection.Replay(new Log().Create(1, "p", "P").Create(2, "a", "A", parent: "p")
                .Move(3, "a", "ghost").Events));

        "Then 'A' stays under 'P'"
            .x(() => tree.ToOutline().ShouldBe("- P\n  - A"));

        "And the move is reported for 'A'"
            .x(() => Reports(tree).ShouldBe([(AnomalyKind.UnknownParent, Log.Id("a"))]));
    }

    [Scenario]
    public void ADR_004_A_move_after_a_sibling_that_does_not_exist_is_not_applied_and_is_reported()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that moves 'A' after an item that was never created"
            .x(() => tree = Projection.Replay(new Log().Create(1, "a", "A").Create(2, "b", "B", after: "a")
                .Move(3, "a", null, after: "ghost").Events));

        "Then 'A' keeps its place before 'B'"
            .x(() => tree.ToOutline().ShouldBe("- A\n- B"));

        "And the move is reported for 'A'"
            .x(() => Reports(tree).ShouldBe([(AnomalyKind.UnknownAfter, Log.Id("a"))]));
    }

    [Scenario]
    public void ADR_004_A_move_after_the_item_itself_is_not_applied_and_is_reported()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that moves 'B' after itself"
            .x(() => tree = Projection.Replay(new Log().Create(1, "a", "A").Create(2, "b", "B", after: "a")
                .Move(3, "b", null, after: "b").Events));

        "Then 'B' keeps its place after 'A'"
            .x(() => tree.ToOutline().ShouldBe("- A\n- B"));

        "And the move is reported for 'B'"
            .x(() => Reports(tree).ShouldBe([(AnomalyKind.UnknownAfter, Log.Id("b"))]));
    }

    [Scenario]
    public void ADR_004_A_new_item_under_a_parent_that_does_not_exist_goes_to_top_level_and_is_reported()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that creates 'A' under an item that was never created"
            .x(() => tree = Projection.Replay(new Log().Create(1, "x", "X").Create(2, "a", "A", parent: "ghost").Events));

        "Then 'A' stands first at top level"
            .x(() => tree.ToOutline().ShouldBe("- A\n- X"));

        "And the creation is reported for 'A'"
            .x(() => Reports(tree).ShouldBe([(AnomalyKind.UnknownParent, Log.Id("a"))]));
    }

    [Scenario]
    public void ADR_004_A_new_item_after_a_sibling_that_does_not_exist_goes_first_and_is_reported()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that creates 'B' under 'P' after an item that was never created"
            .x(() => tree = Projection.Replay(new Log().Create(1, "p", "P").Create(2, "a", "A", parent: "p")
                .Create(3, "b", "B", parent: "p", after: "ghost").Events));

        "Then 'B' is the first child of 'P'"
            .x(() => tree.ToOutline().ShouldBe("- P\n  - B\n  - A"));

        "And the creation is reported for 'B'"
            .x(() => Reports(tree).ShouldBe([(AnomalyKind.UnknownAfter, Log.Id("b"))]));
    }

    [Scenario]
    public void ADR_004_A_sibling_under_another_parent_places_the_item_first_without_a_report()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that creates 'X' under 'Q' after 'Y', which lives under 'P'"
            .x(() => tree = Projection.Replay(new Log().Create(1, "p", "P").Create(2, "y", "Y", parent: "p")
                .Create(3, "q", "Q", after: "p").Create(4, "k", "K", parent: "q")
                .Create(5, "x", "X", parent: "q", after: "y").Events));

        "Then 'X' is the first child of 'Q'"
            .x(() => tree.ToOutline().ShouldBe("- P\n  - Y\n- Q\n  - X\n  - K"));

        "And nothing is reported"
            .x(() => tree.Anomalies.ShouldBeEmpty());
    }

    [Scenario]
    public void ADR_016_An_event_of_an_unknown_type_is_skipped_and_reported()
    {
        Tree tree = default!;

        "Given a log with one item and an event of a type this app does not know"
            .x(() => { });

        "When the tree is rebuilt"
            .x(() => tree = Projection.Replay([.. new Log().Create(1, "a", "A").Events,
                new FutureEvent("e-future", "a0000000-0000-0000-0000-000000000001", new EventClock(2), "action-f")]));

        "Then the tree is unchanged"
            .x(() => tree.ToOutline().ShouldBe("- A"));

        "And the event is reported, with no item"
            .x(() => Reports(tree).ShouldBe([(AnomalyKind.UnknownEventType, (ItemId?)null)]));
    }

    [Scenario]
    public void Reports_are_grouped_by_item_in_item_id_order_with_reports_without_an_item_last()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log with problems for items 'b' and 'a', in that order, and one unknown event"
            .x(() => tree = Projection.Replay([.. new Log().Create(1, "a", "A").Create(2, "b", "B")
                .Move(3, "b", "ghost").Move(4, "a", "ghost").Move(5, "b", null, after: "ghost").Events,
                new FutureEvent("e-future", "a0000000-0000-0000-0000-000000000001", new EventClock(6), "action-f")]));

        "Then the reports for 'a' come first, then those for 'b' in replay order, then the one without an item"
            .x(() => Reports(tree).ShouldBe(
            [
                (AnomalyKind.UnknownParent, Log.Id("a")),
                (AnomalyKind.UnknownParent, Log.Id("b")),
                (AnomalyKind.UnknownAfter, Log.Id("b")),
                (AnomalyKind.UnknownEventType, (ItemId?)null),
            ]));
    }

    private sealed record FutureEvent(string EventId, string DeviceId, EventClock Clock, string ActionId)
        : TreeEvent(EventId, DeviceId, Clock, ActionId);
}
