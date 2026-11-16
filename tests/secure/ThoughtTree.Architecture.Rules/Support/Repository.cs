namespace ThoughtTree.Architecture.Rules.Support;

/// <summary>Locates the repository from the test output folder and lists its files without build output.</summary>
internal static class Repository
{
    public static string Root { get; } = FindRoot();

    public static string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    /// <summary>Files below <paramref name="directory"/> matching the pattern, without bin/ and obj/ folders.</summary>
    public static IReadOnlyList<string> Files(string directory, string searchPattern)
    {
        var start = PathOf(directory);
        if (!Directory.Exists(start))
        {
            return [];
        }

        return Directory.EnumerateFiles(start, searchPattern, SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsBuildOutput(string path)
    {
        var parts = Relative(path).Split('/');
        return parts.Contains("bin") || parts.Contains("obj") || parts.Contains("TestResults");
    }

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
