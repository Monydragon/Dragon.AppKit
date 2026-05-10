using System.Text.Json;
using System.Xml.Linq;

namespace Dragon.AppKit.Publisher.Core;

public sealed class ProjectDetector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public DragonProject Detect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var root = ResolveRoot(path);
        DragonAppConfig? config = null;
        var configPath = Path.Combine(root, "dragon-app.json");
        if (File.Exists(configPath))
        {
            config = JsonSerializer.Deserialize<DragonAppConfig>(File.ReadAllText(configPath), JsonOptions);
        }

        var solutionFiles = Directory.GetFiles(root, "*.slnx", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(root, "*.sln", SearchOption.TopDirectoryOnly))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projectFiles = DetectProjectFiles(root);
        var heads = projectFiles
            .SelectMany(project => project.DetectedTargets)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var publishScriptRelativePath = DetectPublishScript(root);
        var parameters = string.IsNullOrWhiteSpace(publishScriptRelativePath)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : ScriptParameterReader.ReadParameters(Path.Combine(root, publishScriptRelativePath));
        var kind = DetectProjectKind(config, projectFiles, solutionFiles);

        var displayName = FirstNonBlank(config?.ProductName, Path.GetFileName(root), root);
        return new DragonProject(root, displayName, kind, config, solutionFiles, heads, projectFiles, publishScriptRelativePath, parameters);
    }

    private static string ResolveRoot(string path)
    {
        var resolved = Path.GetFullPath(path);
        if (File.Exists(resolved))
        {
            resolved = Path.GetDirectoryName(resolved) ?? resolved;
        }

        var directory = new DirectoryInfo(resolved);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dragon-app.json"))
                || Directory.GetFiles(directory.FullName, "*.slnx", SearchOption.TopDirectoryOnly).Length > 0
                || Directory.GetFiles(directory.FullName, "*.sln", SearchOption.TopDirectoryOnly).Length > 0
                || Directory.GetFiles(directory.FullName, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(path);
    }

    private static IReadOnlyList<DetectedProjectFile> DetectProjectFiles(string root)
    {
        var projects = new List<DetectedProjectFile>();
        foreach (var projectPath in Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsGeneratedPath(projectPath))
            {
                continue;
            }

            projects.Add(ReadProjectFile(root, projectPath));
        }

        return projects
            .OrderBy(project => project.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DetectedProjectFile ReadProjectFile(string root, string projectPath)
    {
        var name = Path.GetFileNameWithoutExtension(projectPath);
        var sdk = string.Empty;
        var targetFrameworks = new List<string>();
        var packageReferences = new List<string>();

        try
        {
            var document = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
            sdk = document.Root?.Attribute("Sdk")?.Value ?? string.Empty;

            foreach (var element in document.Descendants().Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks" or "TargetPlatformIdentifier"))
            {
                foreach (var value in (element.Value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    targetFrameworks.Add(value);
                }
            }

            foreach (var reference in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
            {
                var package = reference.Attribute("Include")?.Value
                    ?? reference.Attribute("Update")?.Value
                    ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(package))
                {
                    packageReferences.Add(package);
                }
            }
        }
        catch
        {
            // A malformed project should still appear in the picker; it just has fewer detected traits.
        }

        var targets = DetectTargets(name, sdk, targetFrameworks, packageReferences);
        return new DetectedProjectFile(
            projectPath,
            Path.GetRelativePath(root, projectPath),
            name,
            sdk,
            targetFrameworks.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            packageReferences.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            targets);
    }

    private static IReadOnlyList<string> DetectTargets(
        string name,
        string sdk,
        IReadOnlyList<string> targetFrameworks,
        IReadOnlyList<string> packageReferences)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var haystack = string.Join(' ', new[] { name, sdk }
            .Concat(targetFrameworks)
            .Concat(packageReferences));

        AddIf(haystack, targets, "Browser", "browser", "wasm", "WebAssembly", ".Browser");
        AddIf(haystack, targets, "Android", "android", ".Android", "Avalonia.Android");
        AddIf(haystack, targets, "iOS", "ios", ".iOS");
        AddIf(haystack, targets, "Windows", "windows", ".Windows", "win-x64");
        AddIf(haystack, targets, "Linux", "linux", ".Linux");
        AddIf(haystack, targets, "macOS", "maccatalyst", "macos", "osx", ".Mac", ".MacOS", ".MacOSX");

        if (name.EndsWith(".Desktop", StringComparison.OrdinalIgnoreCase)
            || packageReferences.Contains("Avalonia.Desktop", StringComparer.OrdinalIgnoreCase))
        {
            targets.Add("Desktop");
        }

        if (targets.Count == 0
            && (sdk.Contains("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase)
                || targetFrameworks.Any(framework => framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))))
        {
            targets.Add("Desktop");
        }

        return targets.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddIf(string haystack, HashSet<string> targets, string target, params string[] needles)
    {
        if (needles.Any(needle => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase)))
        {
            targets.Add(target);
        }
    }

    private static string DetectProjectKind(DragonAppConfig? config, IReadOnlyList<DetectedProjectFile> projects, IReadOnlyList<string> solutions)
    {
        if (config is not null)
        {
            return "Dragon App";
        }

        if (projects.Any(project => project.IsKniOrMonoGame))
        {
            return "KNI/MonoGame App";
        }

        if (projects.Any(project => project.IsMaui))
        {
            return ".NET MAUI App";
        }

        if (projects.Any(project => project.IsAvalonia))
        {
            return "Avalonia/.NET App";
        }

        return projects.Count > 0 || solutions.Count > 0
            ? ".NET Project"
            : "Unknown";
    }

    private static string DetectPublishScript(string root)
    {
        var candidates = new[]
        {
            Path.Combine("scripts", "publish-releases.ps1"),
            Path.Combine("scripts", "publish-release.ps1"),
            "publish-releases.ps1",
            "publish-release.ps1",
            Path.Combine("scripts", "build-release.ps1"),
            "build-release.ps1"
        };

        return candidates.FirstOrDefault(candidate => File.Exists(Path.Combine(root, candidate))) ?? string.Empty;
    }

    private static bool IsGeneratedPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part =>
            part.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || part.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || part.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || part.Equals("artifacts", StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstNonBlank(params string?[] values)
    {
        return values.First(value => !string.IsNullOrWhiteSpace(value))!;
    }
}
