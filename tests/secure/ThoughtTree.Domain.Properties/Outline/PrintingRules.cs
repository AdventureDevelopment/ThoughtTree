using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Properties.Outline;

/// <summary>Printing an event, an item or a tree never shows titles or notes, so logs and error messages carry no content.</summary>
[Trait("Intent", "Secure")]
public sealed class PrintingRules
{
    private const string SecretTitle = "secret-title";
    private const string SecretNote = "secret-note";

    [Fact]
    public void Events_items_and_trees_print_without_titles_or_notes()
    {
        TreeEvent[] events =
        [
            new ItemCreated(new EventId("e1"), new DeviceId("d"), new EventClock(1), new ActionId("a1"), new ItemId("n"), null, null, new Title(SecretTitle), new Note(SecretNote)),
            new ItemTitleChanged(new EventId("e2"), new DeviceId("d"), new EventClock(2), new ActionId("a2"), new ItemId("n"), new Title(SecretTitle)),
            new ItemNoteChanged(new EventId("e3"), new DeviceId("d"), new EventClock(3), new ActionId("a3"), new ItemId("n"), new Note(SecretNote)),
        ];
        var tree = Projection.Replay(events);

        string[] printed = [.. events.Select(treeEvent => treeEvent.ToString()), tree.Find(new ItemId("n"))!.ToString(), tree.ToString()];

        printed.ShouldAllBe(text => !text.Contains(SecretTitle) && !text.Contains(SecretNote));
    }
}
