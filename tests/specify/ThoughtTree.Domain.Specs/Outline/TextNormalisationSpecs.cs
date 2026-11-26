using LambdaTale;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Specs.Outline;

/// <summary>Titles and notes are stored in one normalised form (ADR-029, INV-11).</summary>
[Trait("Intent", "Specify")]
[Trait("ADR", "ADR-029")]
public sealed class TextNormalisationSpecs
{
    private const string AllLineBreaks = "one\r\ntwo\rthree\nfour\u0085five\u2028six\u2029seven\u000Beight\u000Cnine";

    [Scenario]
    public void INV_11_A_title_typed_with_a_combining_accent_is_stored_composed()
    {
        Tree tree = default!;

        "Given a log that creates the item 'Caf\u00E9' typed as 'e' followed by a combining accent"
            .x(() => tree = Projection.Replay(new Log().Create(1, "n", "Cafe\u0301").Events));

        "Then the title holds the composed letter"
            .x(() => TitleOf(tree, "n").ShouldBe("Caf\u00E9"));
    }

    [Scenario]
    public void INV_11_A_note_typed_with_a_combining_accent_is_stored_composed()
    {
        Tree tree = default!;

        "Given a log that creates an item and gives it the note 'Caf\u00E9' typed as 'e' followed by a combining accent"
            .x(() => tree = Projection.Replay(new Log().Create(1, "n", "Menu").Note(2, "n", "Cafe\u0301").Events));

        "Then the note holds the composed letter"
            .x(() => NoteOf(tree, "n").ShouldBe("Caf\u00E9"));
    }

    [Scenario]
    public void INV_11_Every_kind_of_line_break_in_a_title_becomes_one_space()
    {
        Tree tree = default!;

        "Given a log that creates an item whose title has a line break of each kind between its words"
            .x(() => tree = Projection.Replay(new Log().Create(1, "n", AllLineBreaks).Events));

        "Then each line break is one space, a carriage return with a line feed counting as one"
            .x(() => TitleOf(tree, "n").ShouldBe("one two three four five six seven eight nine"));
    }

    [Scenario]
    public void INV_11_A_line_feed_followed_by_a_carriage_return_in_a_title_is_two_line_breaks()
    {
        Tree tree = default!;

        "Given a log that creates an item whose title has a line feed followed by a carriage return"
            .x(() => tree = Projection.Replay(new Log().Create(1, "n", "one\n\rtwo").Events));

        "Then the title has two spaces there"
            .x(() => TitleOf(tree, "n").ShouldBe("one  two"));
    }

    [Scenario]
    public void INV_11_Every_kind_of_line_break_in_a_note_becomes_a_line_feed()
    {
        Tree tree = default!;

        "Given a log that gives an item a note with a line break of each kind between its words"
            .x(() => tree = Projection.Replay(new Log().Create(1, "n", "List").Note(2, "n", AllLineBreaks).Events));

        "Then each line break is one line feed, a carriage return with a line feed counting as one"
            .x(() => NoteOf(tree, "n").ShouldBe("one\ntwo\nthree\nfour\nfive\nsix\nseven\neight\nnine"));
    }

    [Scenario]
    public void INV_11_An_emoji_sequence_stays_as_written()
    {
        const string family = "\U0001F469\u200D\U0001F469\u200D\U0001F467 \U0001F44D\U0001F3FD";
        Tree tree = default!;

        "Given a log that creates an item whose title is a joined family emoji and a thumbs up with a skin tone"
            .x(() => tree = Projection.Replay(new Log().Create(1, "n", family).Events));

        "Then the title is unchanged"
            .x(() => TitleOf(tree, "n").ShouldBe(family));
    }

    private static string TitleOf(Tree tree, string item) => tree.Find(Log.Id(item))!.Title.ToString()!;

    private static string NoteOf(Tree tree, string item) => tree.Find(Log.Id(item))!.Note.ToString()!;
}
