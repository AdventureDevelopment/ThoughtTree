using YamlDotNet.RepresentationModel;

namespace ThoughtTree.Architecture.Rules.Support;

/// <summary>
/// A GitHub Actions workflow parsed as YAML, so that rules read its triggers, permissions, jobs and steps the way
/// GitHub does instead of matching text (flow style, scalar style and comments cannot hide anything). A file that
/// is not valid YAML, or has duplicate keys, fails to load and so fails every rule that reads it.
/// </summary>
internal sealed class Workflow
{
    private Workflow(string path, YamlMappingNode root)
    {
        this.Path = path;
        this.Root = root;
        this.Triggers = ReadTriggers(Child(root, "on"));
        this.Permissions = Child(root, "permissions");
        this.Jobs = Child(root, "jobs") is YamlMappingNode jobs
            ? jobs.Children.Select(job => new WorkflowJob(Scalar(job.Key) ?? string.Empty, job.Value as YamlMappingNode ?? new YamlMappingNode(), this)).ToList()
            : [];
    }

    public string Path { get; }

    public string Name => Repository.Relative(this.Path);

    public YamlMappingNode Root { get; }

    /// <summary>Event names the workflow runs on, from scalar, sequence or mapping style.</summary>
    public IReadOnlySet<string> Triggers { get; }

    /// <summary>The top-level permissions node, or null if there is none.</summary>
    public YamlNode? Permissions { get; }

    public IReadOnlyList<WorkflowJob> Jobs { get; }

    /// <summary>Every workflow file in .github/workflows, both *.yml and *.yaml.</summary>
    public static IReadOnlyList<Workflow> All() =>
        Files().Select(Load).ToList();

    public static IReadOnlyList<string> Files()
    {
        var folder = System.IO.Path.Combine(".github", "workflows");
        return Repository.Files(folder, "*.yml").Concat(Repository.Files(folder, "*.yaml")).Order(StringComparer.Ordinal).ToList();
    }

    public static Workflow Load(string path) => Parse(File.ReadAllText(path), path);

    public static Workflow Parse(string text, string path = "sample.yml")
    {
        var stream = new YamlStream();
        using var reader = new StringReader(text);
        stream.Load(reader);
        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidDataException($"{path}: expected exactly one YAML document with a mapping at its root");
        }

        return new Workflow(path, root);
    }

    /// <summary>Whether the permissions node grants <paramref name="scope"/> write access. A missing node means
    /// the repository default, which may be write: treated as granted.</summary>
    public static bool GrantsWrite(YamlNode? permissions, string scope) => permissions switch
    {
        null => true,
        YamlScalarNode scalar => scalar.Value is "write-all",
        YamlMappingNode mapping => Scalar(Child(mapping, scope)) is "write",
        _ => true,
    };

    public static YamlNode? Child(YamlNode? node, string key) =>
        node is YamlMappingNode mapping && mapping.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;

    public static string? Scalar(YamlNode? node) => (node as YamlScalarNode)?.Value;

    private static HashSet<string> ReadTriggers(YamlNode? on) => on switch
    {
        YamlScalarNode scalar when scalar.Value is not null => [scalar.Value],
        YamlSequenceNode sequence => sequence.Children.Select(Scalar).OfType<string>().ToHashSet(StringComparer.Ordinal),
        YamlMappingNode mapping => mapping.Children.Keys.Select(Scalar).OfType<string>().ToHashSet(StringComparer.Ordinal),
        _ => [],
    };
}

/// <summary>One job of a workflow.</summary>
internal sealed class WorkflowJob(string id, YamlMappingNode node, Workflow workflow)
{
    public string Id { get; } = id;

    public YamlMappingNode Node { get; } = node;

    public Workflow Workflow { get; } = workflow;

    /// <summary>The job's own permissions, or the workflow's when the job has none.</summary>
    public YamlNode? EffectivePermissions => Workflow.Child(this.Node, "permissions") ?? this.Workflow.Permissions;

    public bool CanRequestIdToken => Workflow.GrantsWrite(this.EffectivePermissions, "id-token");

    /// <summary>The GitHub environment name, from `environment: name` or `environment: { name: ... }`.</summary>
    public string? Environment => Workflow.Child(this.Node, "environment") switch
    {
        YamlScalarNode scalar => scalar.Value,
        YamlMappingNode mapping => Workflow.Scalar(Workflow.Child(mapping, "name")),
        _ => null,
    };

    /// <summary>The reusable workflow a job calls (`uses:` at job level), or null.</summary>
    public string? Calls => Workflow.Scalar(Workflow.Child(this.Node, "uses"));

    public IReadOnlyList<WorkflowStep> Steps => Workflow.Child(this.Node, "steps") is YamlSequenceNode steps
        ? steps.Children.OfType<YamlMappingNode>().Select(stepNode => new WorkflowStep(stepNode)).ToList()
        : [];

    public override string ToString() => $"{this.Workflow.Name}: job {this.Id}";
}

/// <summary>One step of a job.</summary>
internal sealed class WorkflowStep(YamlMappingNode node)
{
    public YamlMappingNode Node { get; } = node;

    public string? Uses => Workflow.Scalar(Workflow.Child(this.Node, "uses"));

    public string? Run => Workflow.Scalar(Workflow.Child(this.Node, "run"));

    public string? With(string input) => Workflow.Scalar(Workflow.Child(Workflow.Child(this.Node, "with"), input));

    public bool HasWith(string input) => Workflow.Child(Workflow.Child(this.Node, "with"), input) is not null;
}
