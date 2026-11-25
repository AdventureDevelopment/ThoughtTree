using LambdaTale;
using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

/// <summary>Events the projection does not apply are kept in the log and reported for the user to review (owner decisions 2026-11-25).</summary>
[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-005")]
[Trait("ADR", "ADR-014")]
public class AnomalySpecs
{
    private const string Device = "a0000000-0000-0000-0000-000000000001";

    private static ItemCreated Create(string eventId, long time, string item, string title) =>
        new(eventId, Device, new EventClock(time, 0), "action", new ItemId(item), null, null, title);

    private static ItemTitleChanged Rename(string eventId, long time, string item, string title) =>
        new(eventId, Device, new EventClock(time, 0), "action", new ItemId(item), title);

    [Scenario]
    public void INV_1_Two_events_with_the_same_device_and_clock_apply_in_event_id_order_and_flag_the_later_one_in_any_arrival_order()
    {
        Tree first = default!;
        Tree second = default!;
        TreeEvent create = default!;
        TreeEvent early = default!;
        TreeEvent late = default!;

        "Given one device that wrote two different renames at the same clock, with event ids 'e1' and 'e2'"
            .x(() =>
            {
                create = Create("e0", 1, "n", "Note");
                early = Rename("e1", 5, "n", "One");
                late = Rename("e2", 5, "n", "Two");
            });

        "When the tree is rebuilt with either event arriving first"
            .x(() =>
            {
                first = Projection.Replay([create, early, late]);
                second = Projection.Replay([late, early, create]);
            });

        "Then both renames apply in event id order, so the later id wins, and it is flagged, in both cases"
            .x(() =>
            {
                first.ToOutline().ShouldBe("- Two");
                first.Anomalies.ShouldBe([new Anomaly(AnomalyKind.SameClock, "e2")]);
                TreeText.Full(second).ShouldBe(TreeText.Full(first));
            });
    }

    [Scenario]
    public void INV_5_A_tied_creation_is_flagged_but_its_item_is_not_lost()
    {
        Tree tree = default!;

        "When the tree is rebuilt from two creations of different items by one device at the same clock"
            .x(() => tree = Projection.Replay([Create("e1", 5, "a", "A"), Create("e2", 5, "b", "B")]));

        "Then both items are in the tree and the later event id is flagged"
            .x(() =>
            {
                tree.AllItems.Count.ShouldBe(2);
                tree.Anomalies.ShouldBe([new Anomaly(AnomalyKind.SameClock, "e2")]);
            });
    }

    [Scenario]
    public void A_second_creation_of_an_existing_item_is_reported_and_not_applied()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that creates item 'n' twice with different titles"
            .x(() => tree = Projection.Replay([Create("e1", 1, "n", "First"), Create("e2", 2, "n", "Second")]));

        "Then the first creation stands and the second is reported"
            .x(() =>
            {
                tree.ToOutline().ShouldBe("- First");
                tree.AllItems.Count.ShouldBe(1);
                tree.Anomalies.ShouldBe([new Anomaly(AnomalyKind.DuplicateCreate, "e2")]);
            });
    }

    [Scenario]
    public void A_change_to_an_item_that_does_not_exist_yet_is_reported_and_not_applied()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log whose rename comes earlier in the agreed order than the creation"
            .x(() => tree = Projection.Replay([Rename("e1", 1, "n", "Early"), Create("e2", 2, "n", "Created")]));

        "Then the item keeps its created title and the rename is reported"
            .x(() =>
            {
                tree.ToOutline().ShouldBe("- Created");
                tree.Anomalies.ShouldBe([new Anomaly(AnomalyKind.UnknownItem, "e1")]);
            });
    }

    [Scenario]
    public void A_clean_log_reports_nothing()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log without any problem"
            .x(() => tree = Projection.Replay([Create("e1", 1, "n", "Note"), Rename("e2", 2, "n", "Ideas")]));

        "Then nothing is reported"
            .x(() => tree.Anomalies.ShouldBeEmpty());
    }
}
