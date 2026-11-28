namespace ThoughtTree.Domain.Outline;

/// <summary>One item of the tree, deleted items included (ADR-005).</summary>
public sealed record Item
{
    internal Item(ItemId id, Title title, Note note)
    {
        this.Id = id;
        this.Title = title;
        this.Note = note;
    }

    public ItemId Id { get; private init; }

    public Title Title { get; private init; }

    public Note Note { get; private init; }

    public bool Deleted { get; private init; }

    public ItemId? Parent { get; private init; }

    public override string ToString() => $"Item {this.Id}";

    internal Item WithTitle(Title title) => this with { Title = title };

    internal Item WithNote(Note note) => this with { Note = note };

    internal Item WithDeleted(bool deleted) => this with { Deleted = deleted };

    internal Item WithParent(ItemId? parent) => this with { Parent = parent };
}
