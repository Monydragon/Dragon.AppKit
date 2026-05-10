namespace Dragon.AppKit.Publisher.Core;

public sealed class PublishOptions
{
    public string Configuration { get; set; } = "Release";

    public string Version { get; set; } = string.Empty;

    public string BuildVersion { get; set; } = string.Empty;

    public List<string> Platforms { get; } = [];

    public List<string> DesktopRuntimes { get; } = [];

    public bool SelfContainedDesktop { get; set; } = true;

    public bool SkipTests { get; set; }

    public int AndroidVersionCode { get; set; }

    public string AndroidPackageFormat { get; set; } = "Both";

    public string AndroidSigningMode { get; set; } = "Unsigned";

    public string IosRuntimeIdentifier { get; set; } = "iossimulator-x64";

    public string IosSigningMode { get; set; } = "Unsigned";

    public bool IosArchiveOnBuild { get; set; } = true;

    public bool RunScreenshotQa { get; set; }

    public bool RunCleanSlateQa { get; set; }

    public bool RunPerformanceQa { get; set; }

    public string ArtifactOutputDirectory { get; set; } = string.Empty;

    public string ExtraArguments { get; set; } = string.Empty;

    public bool UpdateVersionMetadata { get; set; } = true;

    public bool PublishToItch { get; set; }

    public string ButlerPath { get; set; } = "butler";

    public string ItchUser { get; set; } = string.Empty;

    public string ItchGame { get; set; } = string.Empty;

    public string ItchChannel { get; set; } = string.Empty;

    public string ItchSourcePath { get; set; } = string.Empty;

    public string ItchExtraArguments { get; set; } = string.Empty;

    public static PublishOptions FromProject(DragonProject project)
    {
        var options = new PublishOptions
        {
            Version = project.Version,
            AndroidVersionCode = project.Config?.AndroidVersionCode ?? 0,
            ButlerPath = FirstNonBlank(project.Config?.Publishing.Itch.ButlerPath, "butler"),
            ItchUser = project.Config?.Publishing.Itch.User ?? string.Empty,
            ItchGame = project.Config?.Publishing.Itch.Game ?? string.Empty,
            ItchChannel = project.Config?.Publishing.Itch.Channel ?? string.Empty,
            ItchSourcePath = project.Config?.Publishing.Itch.SourcePath ?? string.Empty
        };

        options.Platforms.AddRange(
            project.Config?.Release.DefaultPlatforms is { Length: > 0 } platforms
                ? platforms
                : project.SupportedTargets);

        options.DesktopRuntimes.AddRange(
            project.Config?.Release.DefaultDesktopRuntimes is { Length: > 0 } runtimes
                ? runtimes
                : ["win-x64"]);

        return options;
    }

    private static string FirstNonBlank(params string?[] values)
    {
        return values.First(value => !string.IsNullOrWhiteSpace(value))!;
    }
}
