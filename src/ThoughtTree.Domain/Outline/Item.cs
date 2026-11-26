using System.Collections.Immutable;

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

    /// <summary>In order, deleted ones included (ADR-004).</summary>
    public ImmutableList<ItemId> Children { get; private init; } = [];

    // A record prints every property; titles and notes must stay out of logs.
    public override string ToString() => $"Item {this.Id}";

    internal Item WithTitle(string title) => this with { Title = title };

    internal Item WithNote(string note) => this with { Note = note };

    internal Item WithDeleted(bool deleted) => this with { Deleted = deleted };

    internal Item WithParent(ItemId? parent) => this with { Parent = parent };

    internal Item WithChildren(ImmutableList<ItemId> children) => this with { Children = children };
}
