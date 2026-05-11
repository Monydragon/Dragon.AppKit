namespace Dragon.AppKit.Publisher.Core;

public sealed class PublishCommandBuilder
{
    public PublishCommand Build(DragonProject project, PublishOptions options)
    {
        var commands = new List<string>
        {
            project.HasPublishScript
                ? BuildScriptCommand(project, options)
                : BuildDotNetPublishCommand(project, options)
        };

        if (options.PublishToItch)
        {
            commands.Add(BuildItchCommand(project, options));
        }

        return new PublishCommand(project.RootPath, JoinPlanCommands(commands));
    }

    private static string JoinPlanCommands(IReadOnlyList<string> commands)
    {
        var usable = commands.Where(command => !string.IsNullOrWhiteSpace(command)).ToArray();
        if (usable.Length <= 1)
        {
            return usable.FirstOrDefault() ?? string.Empty;
        }

        var guarded = new List<string>();
        for (var index = 0; index < usable.Length; index++)
        {
            guarded.Add(usable[index]);
            if (index < usable.Length - 1)
            {
                guarded.Add("if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }");
            }
        }

        return string.Join(Environment.NewLine, guarded);
    }

    private static string BuildScriptCommand(DragonProject project, PublishOptions options)
    {
        var args = new List<string>();
        AddParameter(project, args, "Configuration", options.Configuration);
        AddParameter(project, args, "Version", options.Version);
        AddParameter(project, args, "BuildVersion", options.BuildVersion);
        AddArrayParameter(project, args, "Platform", options.Platforms);
        AddArrayParameter(project, args, "DesktopRuntime", options.DesktopRuntimes);
        AddBoolParameter(project, args, "SelfContainedDesktop", options.SelfContainedDesktop);
        AddIntParameter(project, args, "AndroidVersionCode", options.AndroidVersionCode);
        AddParameter(project, args, "AndroidPackageFormat", options.AndroidPackageFormat);
        AddParameter(project, args, "AndroidSigningMode", options.AndroidSigningMode);
        AddParameter(project, args, "IosRuntimeIdentifier", options.IosRuntimeIdentifier);
        AddParameter(project, args, "IosSigningMode", options.IosSigningMode);
        AddBoolParameter(project, args, "IosArchiveOnBuild", options.IosArchiveOnBuild);
        AddParameter(project, args, "ReleaseRoot", options.ArtifactOutputDirectory);
        AddParameter(project, args, "ArtifactOutputDirectory", options.ArtifactOutputDirectory);
        AddSwitch(project, args, "SkipTests", options.SkipTests);
        AddSwitch(project, args, "RunScreenshotQa", options.RunScreenshotQa);
        AddSwitch(project, args, "RunCleanSlateQa", options.RunCleanSlateQa);
        AddSwitch(project, args, "RunPerformanceQa", options.RunPerformanceQa);

        if (!string.IsNullOrWhiteSpace(options.ExtraArguments))
        {
            args.Add(options.ExtraArguments.Trim());
        }

        return $"{ToPowerShellScriptPath(project.PublishScriptRelativePath)} {string.Join(' ', args)}".TrimEnd();
    }

    private static string BuildDotNetPublishCommand(DragonProject project, PublishOptions options)
    {
        var args = new List<string>
        {
            "dotnet publish",
            Quote(project.PrimaryPublishPath),
            "-c",
            Quote(options.Configuration)
        };

        var runtime = options.DesktopRuntimes.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(runtime))
        {
            args.Add("-r");
            args.Add(runtime);
            args.Add("--self-contained");
            args.Add(options.SelfContainedDesktop ? "true" : "false");
        }

        if (!string.IsNullOrWhiteSpace(options.ArtifactOutputDirectory))
        {
            args.Add("-o");
            args.Add(Quote(options.ArtifactOutputDirectory));
        }

        if (!string.IsNullOrWhiteSpace(options.Version))
        {
            args.Add($"/p:Version={Quote(options.Version)}");
            args.Add($"/p:InformationalVersion={Quote(options.Version)}");
            args.Add($"/p:ApplicationDisplayVersion={Quote(options.Version)}");
        }

        if (options.AndroidVersionCode > 0)
        {
            args.Add($"/p:ApplicationVersion={options.AndroidVersionCode}");
        }

        if (!string.IsNullOrWhiteSpace(options.ExtraArguments))
        {
            args.Add(options.ExtraArguments.Trim());
        }

        return string.Join(' ', args);
    }

    private static string BuildItchCommand(DragonProject project, PublishOptions options)
    {
        var butler = FirstNonBlank(options.ButlerPath, "butler");
        var sourcePath = ResolveItchSourcePath(project, options);
        var user = options.ItchUser.Trim();
        var game = options.ItchGame.Trim();
        var channel = FirstNonBlank(options.ItchChannel, InferItchChannel(options));

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(game))
        {
            throw new InvalidOperationException("itch.io publishing needs both itch user and game configured.");
        }

        var args = new List<string>
        {
            $"& {Quote(butler)}",
            "push",
            Quote(sourcePath),
            Quote($"{user}/{game}:{channel}")
        };

        if (!string.IsNullOrWhiteSpace(options.ItchExtraArguments))
        {
            args.Add(options.ItchExtraArguments.Trim());
        }

        return string.Join(' ', args);
    }

    private static string ResolveItchSourcePath(DragonProject project, PublishOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ItchSourcePath))
        {
            return options.ItchSourcePath;
        }

        if (!string.IsNullOrWhiteSpace(options.ArtifactOutputDirectory))
        {
            return options.ArtifactOutputDirectory;
        }

        var releaseVersion = FirstNonBlank(options.Version, project.Version, "current");
        return Path.Combine(project.ReleaseRoot, $"v{releaseVersion}");
    }

    private static string InferItchChannel(PublishOptions options)
    {
        var platform = options.Platforms.FirstOrDefault(platform => !platform.Equals("All", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(platform)
            ? "windows"
            : platform.ToLowerInvariant();
    }

    private static void AddParameter(DragonProject project, List<string> args, string name, string? value)
    {
        if (!Supports(project, name) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        args.Add($"-{name} {Quote(value)}");
    }

    private static void AddArrayParameter(DragonProject project, List<string> args, string name, IReadOnlyCollection<string> values)
    {
        if (!Supports(project, name) || values.Count == 0)
        {
            return;
        }

        args.Add($"-{name} {string.Join(',', values)}");
    }

    private static void AddIntParameter(DragonProject project, List<string> args, string name, int value)
    {
        if (!Supports(project, name) || value <= 0)
        {
            return;
        }

        args.Add($"-{name} {value}");
    }

    private static void AddBoolParameter(DragonProject project, List<string> args, string name, bool value)
    {
        if (!Supports(project, name))
        {
            return;
        }

        args.Add($"-{name}:${value.ToString().ToLowerInvariant()}");
    }

    private static void AddSwitch(DragonProject project, List<string> args, string name, bool enabled)
    {
        if (enabled && Supports(project, name))
        {
            args.Add($"-{name}");
        }
    }

    private static bool Supports(DragonProject project, string name)
    {
        return project.ScriptParameters.Count == 0 || project.ScriptParameters.Contains(name);
    }

    private static string FirstNonBlank(params string?[] values)
    {
        return values.First(value => !string.IsNullOrWhiteSpace(value))!;
    }

    private static string ToPowerShellScriptPath(string relativePath)
    {
        var path = ".\\" + relativePath.Replace('/', '\\');
        return path.Contains(' ', StringComparison.Ordinal)
            ? $"& {Quote(path)}"
            : path;
    }

    private static string Quote(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }
}
