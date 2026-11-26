using LambdaTale;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-002")]
public sealed class SnapshotSpecs
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
            .x(() => projector = log.Events.Aggregate(Projector.Empty, (p, e) => p.Add(e)));

        "Then its tree is the tree of a full replay"
            .x(() => TreeText.Full(projector.Tree).ShouldBe(TreeText.Full(Projection.Replay(log.Events))));
    }

    [Scenario]
    public void INV_7_An_event_that_belongs_before_the_applied_ones_takes_its_place_in_the_agreed_order()
    {
        Log late = default!;
        Log early = default!;
        Projector projector = default!;

        "Given a projector that has applied a rename of 'a' at clock 5"
            .x(() =>
            {
                early = new Log().Create(1, "a", "A").Rename(5, "a", "Later title");
                projector = early.Events.Aggregate(Projector.Empty, (p, e) => p.Add(e));
            });

        "When a rename of 'a' with clock 2 arrives from another device"
            .x(() =>
            {
                late = new Log("b0000000-0000-0000-0000-000000000002").Rename(2, "a", "Earlier title");
                projector = projector.Add(late.Events[0]);
            });

        "Then the later rename still wins, as in a full replay"
            .x(() =>
            {
                projector.Tree.ToOutline().ShouldBe("- Later title");
                TreeText.Full(projector.Tree).ShouldBe(TreeText.Full(Projection.Replay([.. early.Events, .. late.Events])));
            });
    }

    [Scenario]
    public void INV_7_An_event_the_projector_already_has_changes_nothing()
    {
        Log log = default!;
        Projector once = default!;
        Projector twice = default!;

        "Given a projector that has applied one event"
            .x(() =>
            {
                log = new Log().Create(1, "a", "A");
                once = Projector.Empty.Add(log.Events[0]);
            });

        "When the same event arrives again"
            .x(() => twice = once.Add(log.Events[0]));

        "Then the tree is the same"
            .x(() => TreeText.Full(twice.Tree).ShouldBe(TreeText.Full(once.Tree)));
    }

    [Scenario]
    public void INV_6_A_kept_projector_plus_the_events_it_has_not_seen_gives_the_tree_of_a_full_replay()
    {
        Log log = default!;
        Projector kept = default!;
        Projector resumed = default!;

        "Given a log of five events and a projector kept after the first three"
            .x(() =>
            {
                log = new Log().Create(1, "a", "A").Create(2, "b", "B", parent: "a").Rename(3, "a", "A2")
                    .Move(4, "b", null).Delete(5, "a");
                kept = log.Events.Take(3).Aggregate(Projector.Empty, (p, e) => p.Add(e));
            });

        "When the app goes on from the kept projector with the events it has not seen, in reverse order"
            .x(() => resumed = log.Events.Skip(3).Reverse().Aggregate(kept, (p, e) => p.Add(e)));

        "Then the tree is the tree of a full replay"
            .x(() => TreeText.Full(resumed.Tree).ShouldBe(TreeText.Full(Projection.Replay(log.Events))));
    }

    [Scenario]
    public void INV_6_A_kept_projector_does_not_change_when_the_app_goes_on_from_it()
    {
        Projector kept = default!;

        "Given a kept projector with one item"
            .x(() => kept = Projector.Empty.Add(new Log().Create(1, "a", "A").Events[0]));

        "When the app applies a rename to it"
            .x(() => _ = kept.Add(new Log("b0000000-0000-0000-0000-000000000002").Rename(2, "a", "Changed").Events[0]));

        "Then the kept projector still shows the old title"
            .x(() => kept.Tree.ToOutline().ShouldBe("- A"));
    }
}
