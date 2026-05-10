using System.Text.RegularExpressions;

namespace Dragon.AppKit.Publisher.Core;

public static class ScriptParameterReader
{
    public static IReadOnlySet<string> ReadParameters(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var text = File.ReadAllText(scriptPath);
        var start = text.IndexOf("param(", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var end = FindMatchingParen(text, start + "param".Length);
        if (end <= start)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var paramText = text.Substring(start, end - start + 1);
        return Regex.Matches(paramText, @"\$(?<name>[A-Za-z_][A-Za-z0-9_]*)")
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int FindMatchingParen(string text, int openParenIndex)
    {
        var depth = 0;
        for (var i = openParenIndex; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }
}
