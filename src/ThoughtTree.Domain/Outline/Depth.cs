namespace ThoughtTree.Domain.Outline;

/// <summary>How many levels below the top level an item sits in the outline; top-level items are at depth 0.</summary>
public readonly record struct Depth(int Levels)
{
    public static Depth TopLevel { get; } = new(0);

    public Depth Below() => new(this.Levels + 1);
}
