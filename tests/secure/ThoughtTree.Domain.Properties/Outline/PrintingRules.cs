using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Properties.Outline;

/// <summary>Printing an event, an item or a tree never shows titles or notes, so logs and error messages carry no content.</summary>
[Trait("Intent", "Secure")]
public sealed class PrintingRules
{
    private const string Title = "secret-title";
    private const string Note = "secret-note";

    [Fact]
    public void Events_items_and_trees_print_without_titles_or_notes()
    {
        TreeEvent[] events =
        [
            new ItemCreated("e1", "d", new EventClock(1), "a1", new ItemId("n"), null, null, Title, Note),
            new ItemTitleChanged("e2", "d", new EventClock(2), "a2", new ItemId("n"), Title),
            new ItemNoteChanged("e3", "d", new EventClock(3), "a3", new ItemId("n"), Note),
        ];
        var tree = Projection.Replay(events);

        string[] printed = [.. events.Select(e => e.ToString()), tree.Find(new ItemId("n"))!.ToString(), tree.ToString()];

        printed.ShouldAllBe(p => !p.Contains(Title) && !p.Contains(Note));
    }
}
