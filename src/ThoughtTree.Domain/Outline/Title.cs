namespace ThoughtTree.Domain.Outline;

/// <summary>An item's title, single-line and in Unicode NFC whatever it was built from (ADR-029, INV-11).</summary>
public readonly record struct Title
{
    private readonly string? value;

    public Title(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.value = NormalisedText.ForTitle(value);
    }

    public string Text => this.value ?? "";

    public bool Equals(Title other) => string.Equals(this.Text, other.Text, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Text);

    public override string ToString() => this.Text;
}
