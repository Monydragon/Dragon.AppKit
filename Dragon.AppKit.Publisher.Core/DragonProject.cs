namespace Dragon.AppKit.Publisher.Core;

public sealed record DragonProject(
    string RootPath,
    string DisplayName,
    string ProjectKind,
    DragonAppConfig? Config,
    IReadOnlyList<string> SolutionFiles,
    IReadOnlyList<string> ProjectHeads,
    IReadOnlyList<DetectedProjectFile> ProjectFiles,
    string PublishScriptRelativePath,
    IReadOnlySet<string> ScriptParameters)
{
    public bool HasDragonContract => Config is not null;

    public bool HasPublishScript => !string.IsNullOrWhiteSpace(PublishScriptRelativePath) && File.Exists(PublishScriptPath);

    public string PublishScriptPath => string.IsNullOrWhiteSpace(PublishScriptRelativePath)
        ? Path.Combine(RootPath, "scripts", "publish-releases.ps1")
        : Path.Combine(RootPath, PublishScriptRelativePath);

    public string Version => Config?.Version ?? string.Empty;

    public string AppId => Config?.AppId ?? string.Empty;

    public string EnvPrefix => Config?.EnvPrefix ?? string.Empty;

    public IReadOnlyList<string> SupportedTargets =>
        Config?.SupportedTargets is { Length: > 0 } targets
            ? targets
            : ProjectHeads;

    public string PrimaryPublishPath =>
        ProjectFiles.FirstOrDefault(file => file.DetectedTargets.Contains("Desktop", StringComparer.OrdinalIgnoreCase))?.Path
        ?? ProjectFiles.FirstOrDefault(file => file.DetectedTargets.Count > 0)?.Path
        ?? SolutionFiles.Select(file => Path.Combine(RootPath, file)).FirstOrDefault()
        ?? RootPath;

    public string ReleaseRoot =>
        Config?.Paths.TryGetValue("releaseRoot", out var releaseRoot) == true && !string.IsNullOrWhiteSpace(releaseRoot)
            ? releaseRoot
            : Path.Combine("artifacts", "releases");
}
