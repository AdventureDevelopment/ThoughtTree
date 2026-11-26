namespace ThoughtTree.Domain.Outline;

/// <summary>An item's note, in Unicode NFC with every line break stored as a line feed (ADR-029, INV-11).</summary>
public readonly record struct Note
{
    private readonly string? value;

    public Note(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.value = NormalisedText.ForNote(value);
    }

    public string Value => this.value ?? "";

    public bool Equals(Note other) => string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Value);

    public override string ToString() => this.Value;
}
