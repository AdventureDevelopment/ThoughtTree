using LambdaTale;
using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

/// <summary>A missing title or note means "unchanged", never "reset" (ADR-016; owner decision 2026-11-26, card 17).</summary>
[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-016")]
public class MissingTextSpecs
{
    private const string Device = "a0000000-0000-0000-0000-000000000001";

    [Scenario]
    public void A_title_or_note_change_without_a_value_changes_nothing()
    {
        Tree tree = default!;

        "Given the item 'Ideas' with the note 'memo'"
            .x(() => { });

        "When the tree is rebuilt from a log that then changes its title and its note without giving a value"
            .x(() => tree = Projection.Replay(
            [
                new ItemCreated("e1", Device, new EventClock(1), "a1", new ItemId("n"), null, null, "Ideas", "memo"),
                new ItemTitleChanged("e2", Device, new EventClock(2), "a2", new ItemId("n"), null!),
                new ItemNoteChanged("e3", Device, new EventClock(3), "a3", new ItemId("n"), null!),
            ]));

        "Then the title and the note are unchanged"
            .x(() =>
            {
                tree.Find(new ItemId("n"))!.Title.ShouldBe("Ideas");
                tree.Find(new ItemId("n"))!.Note.ShouldBe("memo");
            });
    }

    [Scenario]
    public void A_new_item_without_a_title_or_note_starts_with_empty_text()
    {
        Tree tree = default!;

        "When the tree is rebuilt from a log that creates an item without a title and without a note"
            .x(() => tree = Projection.Replay(
            [
                new ItemCreated("e1", Device, new EventClock(1), "a1", new ItemId("n"), null, null, null!, null!),
            ]));

        "Then its title and note are empty text"
            .x(() =>
            {
                tree.Find(new ItemId("n"))!.Title.ShouldBe("");
                tree.Find(new ItemId("n"))!.Note.ShouldBe("");
            });
    }
}
