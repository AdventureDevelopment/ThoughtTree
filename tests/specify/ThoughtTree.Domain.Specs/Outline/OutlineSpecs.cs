using LambdaTale;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-002")]
[Trait("ADR", "ADR-004")]
[Trait("ADR", "ADR-005")]
[Trait("ADR", "ADR-014")]
[Trait("ADR", "ADR-023")]
public sealed class OutlineSpecs
{
    private const string DeviceA = "a0000000-0000-0000-0000-000000000001";
    private const string DeviceB = "b0000000-0000-0000-0000-000000000002";

    [Scenario]
    public void A_new_item_goes_directly_after_the_sibling_it_names_and_first_when_none_is_named()
    {
        Log log = default!;
        Tree tree = default!;

        "Given a log that creates 'A', then 'C' after 'A', then 'B' after 'A', then 'First' with no sibling"
            .x(() => log = new Log().Create(1, "a", "A").Create(2, "c", "C", after: "a")
                .Create(3, "b", "B", after: "a").Create(4, "f", "First"));

        "When the tree is rebuilt from the log"
            .x(() => tree = Projection.Replay(log.Events));

        "Then the items stand in the order of the references"
            .x(() => tree.ToOutline().ShouldBe("- First\n- A\n- B\n- C"));
    }

    [Scenario]
    public void An_item_created_under_a_parent_is_its_child()
    {
        Log log = default!;
        Tree tree = default!;

        "Given a log that creates 'Ideas' and 'Draft' under it"
            .x(() => log = new Log().Create(1, "ideas", "Ideas").Create(2, "draft", "Draft", parent: "ideas"));

        "When the tree is rebuilt"
            .x(() => tree = Projection.Replay(log.Events));

        "Then 'Draft' is nested below 'Ideas'"
            .x(() => tree.ToOutline().ShouldBe("- Ideas\n  - Draft"));
    }

    [Scenario]
    public void The_latest_rename_in_the_agreed_order_wins()
    {
        Log log = default!;
        Tree tree = default!;

        "Given a log in which the later rename is written before the earlier one"
            .x(() => log = new Log().Create(1, "n", "Note").Rename(9, "n", "Thoughts").Rename(5, "n", "Ideas"));

        "When the tree is rebuilt"
            .x(() => tree = Projection.Replay(log.Events));

        "Then the item carries the title with the higher clock"
            .x(() => tree.ToOutline().ShouldBe("- Thoughts"));
    }

    [Scenario]
    public void Moving_an_item_places_it_under_the_new_parent_after_the_named_sibling()
    {
        Log log = default!;
        Tree tree = default!;

        "Given a log that moves 'X' under 'P' after 'Y'"
            .x(() => log = new Log().Create(1, "p", "P").Create(2, "y", "Y", parent: "p")
                .Create(3, "z", "Z", parent: "p", after: "y").Create(4, "x", "X").Move(5, "x", "p", after: "y"));

        "When the tree is rebuilt"
            .x(() => tree = Projection.Replay(log.Events));

        "Then 'X' stands between 'Y' and 'Z' and is no longer at top level"
            .x(() => tree.ToOutline().ShouldBe("- P\n  - Y\n  - X\n  - Z"));
    }

    [Scenario]
    public void A_sibling_reference_to_an_item_under_another_parent_places_the_item_first()
    {
        Log log = default!;
        Tree tree = default!;

        "Given a log that names a sibling which lives under another parent"
            .x(() => log = new Log().Create(1, "p", "P").Create(2, "q", "Q").Create(3, "y", "Y", parent: "p")
                .Create(4, "k", "K", parent: "q").Create(5, "x", "X", parent: "q", after: "y"));

        "When the tree is rebuilt"
            .x(() => tree = Projection.Replay(log.Events));

        "Then 'X' is the first child of its parent"
            .x(() => tree.ToOutline().ShouldBe("- Q\n  - X\n  - K\n- P\n  - Y"));
    }

    [Scenario]
    public void Two_devices_that_place_an_item_after_the_same_sibling_at_the_same_time_are_ordered_by_event_id()
    {
        Tree fromAThenB = default!;
        Tree fromBThenA = default!;
        Log root = default!;
        Log a = default!;
        Log b = default!;

        "Given a root item and two devices that each create an item after it with the same clock"
            .x(() =>
            {
                root = new Log("c0000000-0000-0000-0000-000000000003").Create(1, "root", "Root");
                a = new Log(DeviceA).Create(5, "fromA", "FromA", after: "root");
                b = new Log(DeviceB).Create(5, "fromB", "FromB", after: "root");
            });

        "When the tree is rebuilt with device A's events first and again with device B's first"
            .x(() =>
            {
                fromAThenB = Projection.Replay(root.Events.Concat(a.Events).Concat(b.Events));
                fromBThenA = Projection.Replay(root.Events.Concat(b.Events).Concat(a.Events));
            });

        "Then the event with the higher event id comes later in the agreed order and sits directly after the root, in both cases"
            .x(() =>
            {
                fromAThenB.ToOutline().ShouldBe("- Root\n- FromB\n- FromA");
                TreeText.Full(fromBThenA).ShouldBe(TreeText.Full(fromAThenB));
            });
    }

    [Scenario]
    public void ADR_005_A_deleted_item_is_hidden_with_its_children_but_kept()
    {
        Log log = default!;
        Tree tree = default!;

        "Given a log that deletes 'Ideas' which has one child"
            .x(() => log = new Log().Create(1, "ideas", "Ideas").Create(2, "draft", "Draft", parent: "ideas")
                .Delete(3, "ideas"));

        "When the tree is rebuilt"
            .x(() => tree = Projection.Replay(log.Events));

        "Then both items are hidden but still in the tree, with the child still under its parent"
            .x(() =>
            {
                tree.ToOutline().ShouldBeEmpty();
                tree.AllItems.Count.ShouldBe(2);
                tree.Find(Log.Id("draft"))!.Parent.ShouldBe(Log.Id("ideas"));
            });
    }

    [Scenario]
    public void ADR_005_A_restored_item_returns_with_its_children()
    {
        Log log = default!;
        Tree tree = default!;

        "Given a log that deletes and then restores 'Ideas' which has one child"
            .x(() => log = new Log().Create(1, "ideas", "Ideas").Create(2, "draft", "Draft", parent: "ideas")
                .Delete(3, "ideas").Restore(4, "ideas"));

        "When the tree is rebuilt"
            .x(() => tree = Projection.Replay(log.Events));

        "Then both items are visible again"
            .x(() => tree.ToOutline().ShouldBe("- Ideas\n  - Draft"));
    }

    [Scenario]
    public void INV_2_An_event_applied_twice_changes_nothing()
    {
        Log log = default!;
        Tree once = default!;
        Tree twice = default!;

        "Given a log with creates, a rename, a move and a delete"
            .x(() => log = new Log().Create(1, "a", "A").Create(2, "b", "B").Rename(3, "a", "A2")
                .Move(4, "b", "a").Delete(5, "b"));

        "When the tree is rebuilt from the log and from the log with every event repeated"
            .x(() =>
            {
                once = Projection.Replay(log.Events);
                twice = Projection.Replay(log.Events.Concat(log.Events));
            });

        "Then both trees are the same in every item, state and text, and nothing is reported"
            .x(() =>
            {
                TreeText.Full(twice).ShouldBe(TreeText.Full(once));
                twice.Anomalies.ShouldBeEmpty();
            });
    }

    [Scenario]
    public void INV_1_The_order_in_which_events_arrive_does_not_change_the_tree()
    {
        Log a = default!;
        Log b = default!;
        Tree forward = default!;
        Tree backward = default!;

        "Given two devices that create, rename, move and delete items concurrently"
            .x(() =>
            {
                a = new Log(DeviceA).Create(1, "x", "X").Create(3, "y", "Y", after: "x").Move(6, "y", "x").Delete(8, "x");
                b = new Log(DeviceB).Create(2, "z", "Z", after: "x").Rename(4, "x", "X2").Note(5, "z", "memo").Restore(9, "x");
            });

        "When the events are replayed in arrival order and in reverse"
            .x(() =>
            {
                forward = Projection.Replay(a.Events.Concat(b.Events));
                backward = Projection.Replay(b.Events.Concat(a.Events).Reverse());
            });

        "Then both trees are the same in every item, state and text"
            .x(() => TreeText.Full(backward).ShouldBe(TreeText.Full(forward)));
    }
}
