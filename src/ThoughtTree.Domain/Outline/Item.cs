using System.Collections.Immutable;

namespace ThoughtTree.Domain.Outline;

/// <summary>One item of the projection, immutable: a change swaps it for a copy. Deleted items stay as tombstones with their children (ADR-005).</summary>
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

    /// <summary>Children in order, deleted ones included (ADR-004: the order lives here, never on the children).</summary>
    public ImmutableList<ItemId> Children { get; private init; } = [];

    // No titles or notes in logs or error messages.
    public override string ToString() => $"Item {this.Id}";

    internal Item WithTitle(string title) => this with { Title = title };

    internal Item WithNote(string note) => this with { Note = note };

    internal Item WithDeleted(bool deleted) => this with { Deleted = deleted };

    internal Item WithParent(ItemId? parent) => this with { Parent = parent };

    internal Item WithChildren(ImmutableList<ItemId> children) => this with { Children = children };
}
