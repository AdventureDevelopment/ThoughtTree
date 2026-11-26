namespace ThoughtTree.Domain.Outline;

/// <summary>One item of the tree, deleted items included (ADR-005).</summary>
public sealed record Item
{
    internal Item(ItemId id, string title, string note)
    {
        this.Id = id;
        this.Title = title;
        this.Note = note;
    }

    public ItemId Id { get; private init; }

    public string Title { get; private init; }

    public string Note { get; private init; }

    public bool Deleted { get; private init; }

    public ItemId? Parent { get; private init; }

    public override string ToString() => $"Item {this.Id}";

    internal Item WithTitle(string title) => this with { Title = title };

    internal Item WithNote(string note) => this with { Note = note };

    internal Item WithDeleted(bool deleted) => this with { Deleted = deleted };

    internal Item WithParent(ItemId? parent) => this with { Parent = parent };
}
