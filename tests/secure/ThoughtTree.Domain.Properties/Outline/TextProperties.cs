using System.Buffers;
using System.Text;
using CsCheck;
using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;
using ThoughtTree.Domain.Properties.Support;

namespace ThoughtTree.Domain.Properties.Outline;

/// <summary>Properties of titles and notes over generated raw text (ADR-029, INV-11).</summary>
[Trait("Intent", "Secure")]
[Trait("ADR", "ADR-029")]
public sealed class TextProperties
{
    private static readonly SearchValues<char> LineBreaks = SearchValues.Create(['\r', '\n', '\u0085', '\u2028', '\u2029', '\u000B', '\u000C']);

    private static readonly string[] Pieces =
    [
        "a", "e", "A", " ", "\u0301", "\u0308", "\u00E9", "\u212B", "\u1100", "\u1161", "\uAC00",
        "\r", "\n", "\r\n", "\u0085", "\u2028", "\u2029", "\u000B", "\u000C",
        "\U0001F469\u200D\U0001F467", "\U0001F44D\U0001F3FD",
        "\uD800", "\uDC00", "\uFFFD",
    ];

    private static readonly string[] LineBreakPieces = ["\r", "\n", "\r\n", "\u0085", "\u2028", "\u2029", "\u000B", "\u000C"];

    private static readonly Gen<string> RawText = Gen.OneOf(TextOf(Pieces), TextOf(LineBreakPieces));

    [Fact]
    public void A_title_or_note_cannot_be_built_from_no_text()
    {
        Should.Throw<ArgumentNullException>(() => new Title(null!));
        Should.Throw<ArgumentNullException>(() => new Note(null!));
    }

    [Fact]
    public void INV_11_a_title_is_NFC_and_has_no_line_break()
    {
        RawText.SampleFixed(raw =>
        {
            var title = new Title(raw).Value;
            title.IsNormalized(NormalizationForm.FormC).ShouldBeTrue();
            title.AsSpan().ContainsAny(LineBreaks).ShouldBeFalse();
        });
    }

    [Fact]
    public void INV_11_a_note_is_NFC_and_has_no_line_break_but_the_line_feed()
    {
        RawText.SampleFixed(raw =>
        {
            var note = new Note(raw).Value;
            note.IsNormalized(NormalizationForm.FormC).ShouldBeTrue();
            note.Replace("\n", "", StringComparison.Ordinal).AsSpan().ContainsAny(LineBreaks).ShouldBeFalse();
        });
    }

    [Fact]
    public void INV_11_normalising_twice_changes_nothing()
    {
        RawText.SampleFixed(raw =>
        {
            var title = new Title(raw);
            var note = new Note(raw);
            new Title(title.Value).Value.ShouldBe(title.Value);
            new Note(note.Value).Value.ShouldBe(note.Value);
        });
    }

    [Fact]
    public void INV_11_each_line_break_becomes_one_space_in_a_title_and_one_line_feed_in_a_note()
    {
        RawText.SampleFixed(raw =>
        {
            var breaks = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Count(c => LineBreaks.Contains(c));
            new Title(raw).Value.Count(c => c == ' ').ShouldBe(raw.Count(c => c == ' ') + breaks);
            new Note(raw).Value.Count(c => c == '\n').ShouldBe(breaks);
        });
    }

    [Fact]
    public void INV_11_every_text_in_every_event_is_normalised()
    {
        RawText.SampleFixed(raw =>
        {
            var (id, device, clock, action, item) = (new EventId("e"), new DeviceId("d"), new EventClock(1), new ActionId("a"), new ItemId("n"));
            var created = new ItemCreated(id, device, clock, action, item, null, null, new Title(raw), new Note(raw));
            var texts = new[]
            {
                created.Title!.Value.Value,
                created.Note!.Value.Value,
                new ItemTitleChanged(id, device, clock, action, item, new Title(raw)).Title!.Value.Value,
                new ItemNoteChanged(id, device, clock, action, item, new Note(raw)).Note!.Value.Value,
            };
            texts.ShouldAllBe(t => t.IsNormalized(NormalizationForm.FormC));
            texts[0].ShouldBe(texts[2]);
            texts[1].ShouldBe(texts[3]);
        });
    }

    [Fact]
    public void INV_11_each_unpaired_surrogate_becomes_one_replacement_character()
    {
        RawText.SampleFixed(raw =>
        {
            var replacements = raw.Count(c => c == '\uFFFD') + UnpairedSurrogates(raw);
            foreach (var text in new[] { new Title(raw).Value, new Note(raw).Value })
            {
                UnpairedSurrogates(text).ShouldBe(0);
                text.Count(c => c == '\uFFFD').ShouldBe(replacements);
            }
        });
    }

    private static int UnpairedSurrogates(string text)
    {
        var unpaired = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsSurrogatePair(text, i))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                unpaired++;
            }
        }

        return unpaired;
    }

    private static Gen<string> TextOf(string[] pieces) =>
        Gen.Int[0, pieces.Length - 1].Array[0, 24].Select(picks => string.Concat(picks.Select(p => pieces[p])));
}
