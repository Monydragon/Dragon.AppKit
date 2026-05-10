namespace Dragon.AppKit.Publisher.Core;

public sealed record DetectedProjectFile(
    string Path,
    string RelativePath,
    string Name,
    string Sdk,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<string> PackageReferences,
    IReadOnlyList<string> DetectedTargets)
{
    public bool IsKniOrMonoGame =>
        PackageReferences.Any(package =>
            package.StartsWith("Kni.", StringComparison.OrdinalIgnoreCase)
            || package.StartsWith("MonoGame", StringComparison.OrdinalIgnoreCase)
            || package.Equals("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase));

    public bool IsAvalonia =>
        PackageReferences.Any(package => package.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase));

    public bool IsMaui =>
        Sdk.Contains("Maui", StringComparison.OrdinalIgnoreCase)
        || PackageReferences.Any(package => package.Contains("Maui", StringComparison.OrdinalIgnoreCase));

    public bool IsBrowser =>
        DetectedTargets.Contains("Browser", StringComparer.OrdinalIgnoreCase);

    public bool IsAndroid =>
        DetectedTargets.Contains("Android", StringComparer.OrdinalIgnoreCase);
}
