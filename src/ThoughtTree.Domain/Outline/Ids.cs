namespace ThoughtTree.Domain.Outline;

/// <summary>The id of an item, lowercase GUID text.</summary>
public readonly record struct ItemId(string Value)
{
    public override string ToString() => this.Value;
}

/// <summary>The id of an event, unique in a tree; replay ties are ordered by it (ADR-014).</summary>
public readonly record struct EventId(string Value)
{
    public override string ToString() => this.Value;
}

/// <summary>The id of the device that wrote an event.</summary>
public readonly record struct DeviceId(string Value)
{
    public override string ToString() => this.Value;
}

/// <summary>The id shared by all events of one user action (ADR-067).</summary>
public readonly record struct ActionId(string Value)
{
    public override string ToString() => this.Value;
}
