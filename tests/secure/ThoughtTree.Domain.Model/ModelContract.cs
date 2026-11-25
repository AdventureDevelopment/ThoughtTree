namespace ThoughtTree.Domain.Model;

/// <summary>
/// The contract between the reference model and the property tests. The model returns the whole tree as
/// canonical text so two trees are equal exactly when the texts are equal.
/// </summary>
public static class ReferenceModel
{
    /// <summary>Sorts all events by the agreed order, drops duplicate event ids, replays one by one.</summary>
    public static string ReplayToCanonicalText(IEnumerable<Domain.Outline.TreeEvent> events) =>
        throw new NotImplementedException("Written by the reference-model agent.");
}
