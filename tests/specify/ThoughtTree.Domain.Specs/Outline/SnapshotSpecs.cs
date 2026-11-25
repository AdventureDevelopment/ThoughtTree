using LambdaTale;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-002")]
public class SnapshotSpecs
{
    [Scenario]
    public void INV_7_Events_that_arrive_in_order_are_applied_one_at_a_time()
    {
        Log log = default!;
        Projector projector = default!;

        "Given a log of creates, a rename, a move and a delete"
            .x(() => log = new Log().Create(1, "a", "A").Create(2, "b", "B").Rename(3, "a", "A2")
                .Move(4, "b", "a").Delete(5, "b"));

        "When a projector takes the events one at a time in agreed order"
            .x(() =>
            {
                projector = new Projector();
                foreach (TreeEvent e in log.Events)
                {
                    projector.TryAdd(e).ShouldBeTrue();
                }
            });

        "Then its tree is the tree of a full replay"
            .x(() => TreeText.Full(projector.Tree).ShouldBe(TreeText.Full(Projection.Replay(log.Events))));
    }

    [Scenario]
    public void INV_7_An_event_that_belongs_before_the_applied_ones_asks_for_a_rebuild_and_changes_nothing()
    {
        Log log = default!;
        Projector projector = default!;
        bool accepted = true;
        string before = "";

        "Given a projector that has applied events up to clock 5"
            .x(() =>
            {
                log = new Log().Create(5, "a", "A");
                projector = new Projector();
                projector.TryAdd(log.Events[0]);
                before = TreeText.Full(projector.Tree);
            });

        "When an event with clock 2 arrives"
            .x(() => accepted = projector.TryAdd(new Log("b0000000-0000-0000-0000-000000000002").Create(2, "z", "Z").Events[0]));

        "Then the projector refuses it, so the caller rebuilds from the log, and its tree is unchanged"
            .x(() =>
            {
                accepted.ShouldBeFalse();
                TreeText.Full(projector.Tree).ShouldBe(before);
            });
    }

    [Scenario]
    public void INV_7_An_event_the_projector_already_has_is_accepted_and_changes_nothing()
    {
        Log log = default!;
        Projector projector = default!;

        "Given a projector that has applied one event"
            .x(() =>
            {
                log = new Log().Create(1, "a", "A");
                projector = new Projector();
                projector.TryAdd(log.Events[0]);
            });

        "When the same event arrives again"
            .x(() => projector.TryAdd(log.Events[0]).ShouldBeTrue());

        "Then the tree still has one item"
            .x(() => projector.Tree.AllItems.Count.ShouldBe(1));
    }

    [Scenario]
    public void INV_6_A_snapshot_plus_the_events_after_it_gives_the_tree_of_a_full_replay()
    {
        Log log = default!;
        Snapshot snapshot = default!;
        Projector? resumed = null;

        "Given a log of five events and a snapshot taken after the first three"
            .x(() =>
            {
                log = new Log().Create(1, "a", "A").Create(2, "b", "B", parent: "a").Rename(3, "a", "A2")
                    .Move(4, "b", null).Delete(5, "a");
                var projector = new Projector();
                foreach (TreeEvent e in log.Events.Take(3))
                {
                    projector.TryAdd(e);
                }

                snapshot = projector.TakeSnapshot();
            });

        "When the app resumes from the snapshot with the events it has not seen"
            .x(() => resumed = Projector.TryResume(snapshot, log.Events.Skip(3)));

        "Then the tree is the tree of a full replay"
            .x(() => TreeText.Full(resumed!.Tree).ShouldBe(TreeText.Full(Projection.Replay(log.Events))));
    }

    [Scenario]
    public void INV_6_A_snapshot_does_not_change_when_the_projector_it_came_from_goes_on()
    {
        Projector projector = default!;
        Snapshot snapshot = default!;

        "Given a snapshot of a projector with one item"
            .x(() =>
            {
                projector = new Projector();
                projector.TryAdd(new Log().Create(1, "a", "A").Events[0]);
                snapshot = projector.TakeSnapshot();
            });

        "When the projector applies a rename afterwards"
            .x(() => projector.TryAdd(new Log("b0000000-0000-0000-0000-000000000002").Rename(2, "a", "Changed").Events[0]));

        "Then a projector resumed from the snapshot still shows the old title"
            .x(() => Projector.TryResume(snapshot, [])!.Tree.ToOutline().ShouldBe("- A"));
    }

    [Scenario]
    public void INV_6_A_snapshot_is_stale_when_an_unseen_event_belongs_before_it()
    {
        Snapshot snapshot = default!;
        Projector? resumed = new Projector();

        "Given a snapshot taken after an event with clock 5"
            .x(() =>
            {
                var projector = new Projector();
                projector.TryAdd(new Log().Create(5, "a", "A").Events[0]);
                snapshot = projector.TakeSnapshot();
            });

        "When the app resumes with an unseen event that has clock 2"
            .x(() => resumed = Projector.TryResume(snapshot, new Log("b0000000-0000-0000-0000-000000000002").Create(2, "z", "Z").Events));

        "Then the snapshot is refused, so the app rebuilds from the full log"
            .x(() => resumed.ShouldBeNull());
    }
}
