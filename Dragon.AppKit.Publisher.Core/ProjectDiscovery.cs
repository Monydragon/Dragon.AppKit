namespace Dragon.AppKit.Publisher.Core;

public sealed class ProjectDiscovery
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".idea",
        ".vs",
        "artifacts",
        "bin",
        "obj",
        "templates",
        "docs-template",
        "build",
        "node_modules",
        ".gradle"
    };

    private readonly ProjectDetector _detector = new();

    public IReadOnlyList<DragonProject> Discover(string workspaceRoot, int maxDepth = 3)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
        {
            return [];
        }

        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Scan(new DirectoryInfo(workspaceRoot), depth: 0);

        return roots
            .Select(root => _detector.Detect(root))
            .OrderBy(project => project.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        void Scan(DirectoryInfo directory, int depth)
        {
            if (depth > maxDepth || ExcludedDirectoryNames.Contains(directory.Name))
            {
                return;
            }

            if (IsProjectRoot(directory.FullName))
            {
                roots.Add(directory.FullName);
                return;
            }

            foreach (var child in directory.EnumerateDirectories())
            {
                Scan(child, depth + 1);
            }
        }
    }

    private static bool IsProjectRoot(string path)
    {
        return File.Exists(Path.Combine(path, "dragon-app.json"))
            || Directory.GetFiles(path, "*.slnx", SearchOption.TopDirectoryOnly).Length > 0
            || Directory.GetFiles(path, "*.sln", SearchOption.TopDirectoryOnly).Length > 0
            || Directory.GetFiles(path, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0;
    }
}
