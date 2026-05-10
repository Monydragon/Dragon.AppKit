namespace Dragon.AppKit.Publisher.Core;

public sealed record PublishCommand(
    string WorkingDirectory,
    string DisplayText)
{
    public string ToPowerShellCommandText(string? overrideDisplayText = null)
    {
        var command = string.IsNullOrWhiteSpace(overrideDisplayText)
            ? DisplayText
            : overrideDisplayText.Trim();

        return $"Set-Location -LiteralPath {Quote(WorkingDirectory)}; {command}";
    }

    private static string Quote(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }
}
