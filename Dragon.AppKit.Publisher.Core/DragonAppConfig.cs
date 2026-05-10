using System.Text.Json.Serialization;

namespace Dragon.AppKit.Publisher.Core;

public sealed class DragonAppConfig
{
    [JsonPropertyName("productName")]
    public string ProductName { get; init; } = string.Empty;

    [JsonPropertyName("shortName")]
    public string ShortName { get; init; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; init; } = string.Empty;

    [JsonPropertyName("appId")]
    public string AppId { get; init; } = string.Empty;

    [JsonPropertyName("envPrefix")]
    public string EnvPrefix { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("androidVersionCode")]
    public int AndroidVersionCode { get; init; }

    [JsonPropertyName("supportedTargets")]
    public string[] SupportedTargets { get; init; } = [];

    [JsonPropertyName("paths")]
    public Dictionary<string, string> Paths { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("release")]
    public DragonReleaseDefaults Release { get; init; } = new();

    [JsonPropertyName("publishing")]
    public DragonPublishingConfig Publishing { get; init; } = new();
}

public sealed class DragonReleaseDefaults
{
    [JsonPropertyName("defaultPlatforms")]
    public string[] DefaultPlatforms { get; init; } = [];

    [JsonPropertyName("defaultDesktopRuntimes")]
    public string[] DefaultDesktopRuntimes { get; init; } = [];
}

public sealed class DragonPublishingConfig
{
    [JsonPropertyName("itch")]
    public DragonItchPublishingConfig Itch { get; init; } = new();
}

public sealed class DragonItchPublishingConfig
{
    [JsonPropertyName("user")]
    public string User { get; init; } = string.Empty;

    [JsonPropertyName("game")]
    public string Game { get; init; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; init; } = string.Empty;

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; init; } = string.Empty;

    [JsonPropertyName("butlerPath")]
    public string ButlerPath { get; init; } = string.Empty;
}
