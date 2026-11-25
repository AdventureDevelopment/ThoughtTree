using LambdaTale;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-002")]
[Trait("ADR", "ADR-004")]
[Trait("ADR", "ADR-005")]
[Trait("ADR", "ADR-023")]
public class OutlineSpecs
{
    [Scenario]
    public void A_new_item_goes_directly_after_the_sibling_it_names_and_first_when_none_is_named()
    {
        Tree tree = default!;

        "Given a log that creates 'A', then 'C' after 'A', then 'B' after 'A', then 'First' with no sibling"
            .x(() => { });

        "When the tree is rebuilt from the log"
            .x(() => tree = Projection.Replay(new Log()
                .Create(1, "a", "A").Create(2, "c", "C", after: "a")
                .Create(3, "b", "B", after: "a").Create(4, "f", "First").Events));

        "Then the items stand in the order of the references"
            .x(() => tree.ToOutline().ShouldBe("- First\n- A\n- B\n- C"));
    }

    [Scenario]
    public void An_item_created_under_a_parent_is_its_child()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that creates 'Ideas' and 'Draft' under it"
            .x(() => tree = Projection.Replay(new Log()
                .Create(1, "ideas", "Ideas").Create(2, "draft", "Draft", parent: "ideas").Events));

        "Then 'Draft' is nested below 'Ideas'"
            .x(() => tree.ToOutline().ShouldBe("- Ideas\n  - Draft"));
    }

    [Scenario]
    public void The_latest_rename_in_the_agreed_order_wins()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log in which the later rename is written first"
            .x(() => tree = Projection.Replay(new Log()
                .Create(1, "n", "Note").Rename(9, "n", "Thoughts").Rename(5, "n", "Ideas").Events));

        "Then the item carries the title with the higher clock"
            .x(() => tree.ToOutline().ShouldBe("- Thoughts"));
    }

    [Scenario]
    public void Moving_an_item_places_it_under_the_new_parent_after_the_named_sibling()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that moves 'X' under 'P' after 'Y'"
            .x(() => tree = Projection.Replay(new Log()
                .Create(1, "p", "P").Create(2, "y", "Y", parent: "p").Create(3, "z", "Z", parent: "p", after: "y")
                .Create(4, "x", "X").Move(5, "x", "p", after: "y").Events));

        "Then 'X' stands between 'Y' and 'Z' and is no longer at top level"
            .x(() => tree.ToOutline().ShouldBe("- P\n  - Y\n  - X\n  - Z"));
    }

    [Scenario]
    public void A_sibling_reference_to_an_item_under_another_parent_places_the_item_first()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that names a sibling which lives under another parent"
            .x(() => tree = Projection.Replay(new Log()
                .Create(1, "p", "P").Create(2, "q", "Q").Create(3, "y", "Y", parent: "p")
                .Create(4, "k", "K", parent: "q").Create(5, "x", "X", parent: "q", after: "y").Events));

        "Then 'X' is the first child of its parent"
            .x(() => tree.ToOutline().ShouldBe("- Q\n  - X\n  - K\n- P\n  - Y"));
    }

    [Scenario]
    public void ADR_023_A_move_that_would_create_a_cycle_puts_the_item_at_top_level()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that moves 'Parent' under its own child"
            .x(() => tree = Projection.Replay(new Log()
                .Create(1, "parent", "Parent").Create(2, "child", "Child", parent: "parent")
                .Move(3, "parent", "child").Events));

        "Then 'Parent' stands at top level with its subtree and no item is lost"
            .x(() =>
            {
                tree.ToOutline().ShouldBe("- Parent\n  - Child");
                tree.Find(Log.Id("parent"))!.Parent.ShouldBeNull();
            });
    }

    [Scenario]
    public void ADR_005_A_deleted_item_is_hidden_with_its_children_but_kept_and_restorable()
    {
        Tree deleted = default!;
        Tree restored = default!;

        "Given a log that deletes 'Ideas' which has one child"
            .x(() => { });

        "When the tree is rebuilt"
            .x(() => deleted = Projection.Replay(new Log()
                .Create(1, "ideas", "Ideas").Create(2, "draft", "Draft", parent: "ideas").Delete(3, "ideas").Events));

        "And the log restores 'Ideas' and the tree is rebuilt again"
            .x(() => restored = Projection.Replay(new Log()
                .Create(1, "ideas", "Ideas").Create(2, "draft", "Draft", parent: "ideas")
                .Delete(3, "ideas").Restore(4, "ideas").Events));

        "Then both items are hidden but still in the tree after the delete"
            .x(() =>
            {
                deleted.ToOutline().ShouldBeEmpty();
                deleted.AllItems.Count.ShouldBe(2);
            });

        "And both are back after the restore"
            .x(() => restored.ToOutline().ShouldBe("- Ideas\n  - Draft"));
    }

    [Scenario]
    public void INV_2_An_event_applied_twice_changes_nothing()
    {
        var log = new Log().Create(1, "a", "A").Rename(2, "a", "B");
        Tree once = default!;
        Tree twice = default!;

        "When the tree is rebuilt from the log and from the log with every event repeated"
            .x(() =>
            {
                once = Projection.Replay(log.Events);
                twice = Projection.Replay(log.Events.Concat(log.Events));
            });

        "Then both trees are the same"
            .x(() => twice.ToOutline().ShouldBe(once.ToOutline()));
    }

    [Scenario]
    public void INV_1_The_order_in_which_events_arrive_does_not_change_the_tree()
    {
        var a = new Log("a0000000-0000-0000-0000-000000000001").Create(1, "x", "X").Create(3, "y", "Y", after: "x");
        var b = new Log("b0000000-0000-0000-0000-000000000002").Create(2, "z", "Z", after: "x").Rename(4, "x", "X2");
        Tree forward = default!;
        Tree backward = default!;

        "When two devices' events are replayed in arrival order and in reverse"
            .x(() =>
            {
                forward = Projection.Replay(a.Events.Concat(b.Events));
                backward = Projection.Replay(b.Events.Concat(a.Events).Reverse());
            });

        "Then both trees are the same"
            .x(() => backward.ToOutline().ShouldBe(forward.ToOutline()));
    }
}
