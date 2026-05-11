using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Dragon.AppKit.Publisher.Core;

public enum ReleaseVersionScanStatus
{
    MissingExpectedVersion,
    MissingReleaseRoot,
    NoReleaseFolders,
    NoVersionEvidence,
    Consistent,
    Mismatch
}

public sealed record ReleaseVersionEvidence(string Source, string Version);

public sealed record ReleaseVersionScanResult(
    string ExpectedVersion,
    string ReleaseRootPath,
    string ScannedReleasePath,
    bool ScannedExplicitOutputDirectory,
    IReadOnlyList<ReleaseVersionEvidence> Evidence,
    ReleaseVersionScanStatus Status,
    string Message)
{
    public bool BlocksPublish => Status == ReleaseVersionScanStatus.Mismatch;
}

public sealed partial class ReleaseVersionScanner
{
    public ReleaseVersionScanResult ScanLatest(DragonProject project, PublishOptions options)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);

        var expectedVersion = NormalizeVersion(FirstNonBlank(options.Version, project.Version));
        var explicitOutput = !string.IsNullOrWhiteSpace(options.ArtifactOutputDirectory);
        var releaseRoot = ResolveReleaseRoot(project, options);

        if (string.IsNullOrWhiteSpace(expectedVersion))
        {
            return CreateResult(
                expectedVersion,
                releaseRoot,
                string.Empty,
                explicitOutput,
                [],
                ReleaseVersionScanStatus.MissingExpectedVersion,
                "No version selected for release folder verification.");
        }

        if (!Directory.Exists(releaseRoot))
        {
            return CreateResult(
                expectedVersion,
                releaseRoot,
                string.Empty,
                explicitOutput,
                [],
                ReleaseVersionScanStatus.MissingReleaseRoot,
                $"Release root not found: {releaseRoot}");
        }

        var releasePath = explicitOutput || LooksLikeReleaseDirectory(releaseRoot)
            ? releaseRoot
            : FindLatestReleaseDirectory(releaseRoot);

        if (string.IsNullOrWhiteSpace(releasePath))
        {
            return CreateResult(
                expectedVersion,
                releaseRoot,
                string.Empty,
                explicitOutput,
                [],
                ReleaseVersionScanStatus.NoReleaseFolders,
                $"No release folders found under {releaseRoot}");
        }

        var evidence = ReadVersionEvidence(releasePath);
        if (evidence.Count == 0)
        {
            return CreateResult(
                expectedVersion,
                releaseRoot,
                releasePath,
                explicitOutput,
                evidence,
                ReleaseVersionScanStatus.NoVersionEvidence,
                $"No version evidence found in {DescribeScanTarget(explicitOutput)}: {releasePath}");
        }

        var mismatches = evidence
            .Where(item => !VersionsMatch(item.Version, expectedVersion))
            .ToArray();

        if (mismatches.Length > 0)
        {
            var details = string.Join(", ", evidence.Select(item => $"{item.Source}={item.Version}"));
            return CreateResult(
                expectedVersion,
                releaseRoot,
                releasePath,
                explicitOutput,
                evidence,
                ReleaseVersionScanStatus.Mismatch,
                $"Release version mismatch. Expected {expectedVersion}; found {details} in {releasePath}");
        }

        return CreateResult(
            expectedVersion,
            releaseRoot,
            releasePath,
            explicitOutput,
            evidence,
            ReleaseVersionScanStatus.Consistent,
            $"{DescribeScanTarget(explicitOutput)} matches version {expectedVersion}: {releasePath}");
    }

    public static string ResolveReleaseRoot(DragonProject project, PublishOptions options)
    {
        var configuredRoot = FirstNonBlank(options.ArtifactOutputDirectory, project.ReleaseRoot);
        var resolvedRoot = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(project.RootPath, configuredRoot);

        return Path.GetFullPath(resolvedRoot);
    }

    private static ReleaseVersionScanResult CreateResult(
        string expectedVersion,
        string releaseRoot,
        string releasePath,
        bool explicitOutput,
        IReadOnlyList<ReleaseVersionEvidence> evidence,
        ReleaseVersionScanStatus status,
        string message)
    {
        return new ReleaseVersionScanResult(
            expectedVersion,
            releaseRoot,
            releasePath,
            explicitOutput,
            evidence,
            status,
            message);
    }

    private static string FindLatestReleaseDirectory(string releaseRoot)
    {
        var latestAlias = Path.Combine(releaseRoot, "latest");
        if (Directory.Exists(latestAlias))
        {
            return latestAlias;
        }

        return Directory.GetDirectories(releaseRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .ThenByDescending(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
            .Select(directory => directory.FullName)
            .FirstOrDefault() ?? string.Empty;
    }

    private static bool LooksLikeReleaseDirectory(string path)
    {
        return File.Exists(Path.Combine(path, "release-manifest.json"))
            || TryExtractVersion(Path.GetFileName(path), out _);
    }

    private static IReadOnlyList<ReleaseVersionEvidence> ReadVersionEvidence(string releasePath)
    {
        var evidence = new List<ReleaseVersionEvidence>();
        if (TryExtractVersion(Path.GetFileName(releasePath), out var folderVersion))
        {
            evidence.Add(new ReleaseVersionEvidence("folder", folderVersion));
        }

        var manifestPath = Path.Combine(releasePath, "release-manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject();
                AddJsonEvidence(evidence, manifest, "version", "release-manifest.version");
                AddJsonEvidence(evidence, manifest, "tag", "release-manifest.tag");
            }
            catch
            {
                evidence.Add(new ReleaseVersionEvidence("release-manifest.json", "unreadable"));
            }
        }

        return evidence
            .GroupBy(item => $"{item.Source}\0{item.Version}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static void AddJsonEvidence(List<ReleaseVersionEvidence> evidence, JsonObject? manifest, string propertyName, string source)
    {
        if (manifest is null || manifest[propertyName] is not JsonValue value)
        {
            return;
        }

        if (value.TryGetValue<string>(out var rawVersion) && !string.IsNullOrWhiteSpace(rawVersion))
        {
            evidence.Add(new ReleaseVersionEvidence(source, NormalizeVersion(rawVersion)));
        }
    }

    private static bool TryExtractVersion(string value, out string version)
    {
        version = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = VersionPattern().Match(value);
        if (!match.Success)
        {
            return false;
        }

        version = NormalizeVersion(match.Groups["version"].Value);
        return true;
    }

    private static bool VersionsMatch(string actual, string expected)
    {
        return string.Equals(NormalizeVersion(actual), NormalizeVersion(expected), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string? version)
    {
        var value = (version ?? string.Empty).Trim();
        if (value.Length > 1 && value[0] is 'v' or 'V' && char.IsDigit(value[1]))
        {
            value = value[1..];
        }

        return value;
    }

    private static string FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string DescribeScanTarget(bool explicitOutput)
    {
        return explicitOutput ? "Selected release folder" : "Latest release folder";
    }

    [GeneratedRegex(@"(?:^|[^0-9A-Za-z])v?(?<version>\d+(?:\.\d+){1,3}(?:[-+][0-9A-Za-z.-]+)?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
