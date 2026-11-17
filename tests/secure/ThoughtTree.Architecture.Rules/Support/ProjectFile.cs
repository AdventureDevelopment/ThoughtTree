using System.Xml.Linq;

namespace ThoughtTree.Architecture.Rules.Support;

/// <summary>The parts of a .csproj file the dependency rules look at.</summary>
internal sealed record ProjectFile(
    string Name,
    string Path,
    string Sdk,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences,
    IReadOnlyList<string> FrameworkReferences)
{
    public static ProjectFile Load(string path)
    {
        var document = XDocument.Load(path);
        var root = document.Root ?? throw new InvalidOperationException("Empty project file: " + path);

        IReadOnlyList<string> Includes(string element) => root
            .Descendants(element)
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .ToList();

        return new ProjectFile(
            System.IO.Path.GetFileNameWithoutExtension(path),
            path,
            (string?)root.Attribute("Sdk") ?? string.Empty,
            Includes("ProjectReference").Select(r => System.IO.Path.GetFileNameWithoutExtension(r.Replace('\\', '/'))).ToList(),
            Includes("PackageReference"),
            Includes("FrameworkReference"));
    }

    public static IReadOnlyList<ProjectFile> All(string directory) =>
        Repository.Files(directory, "*.csproj").Select(Load).ToList();
}
