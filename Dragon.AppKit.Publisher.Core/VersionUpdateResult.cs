namespace Dragon.AppKit.Publisher.Core;

public sealed record VersionUpdateResult(IReadOnlyList<string> UpdatedFiles)
{
    public bool Changed => UpdatedFiles.Count > 0;
}
