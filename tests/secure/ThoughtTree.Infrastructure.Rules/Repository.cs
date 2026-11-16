namespace ThoughtTree.Infrastructure.Rules;

/// <summary>Locates the repository from the test output folder.</summary>
internal static class Repository
{
    public static string Root { get; } = FindRoot();

    public static string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ThoughtTree.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("ThoughtTree.slnx not found above " + AppContext.BaseDirectory);
    }
}
