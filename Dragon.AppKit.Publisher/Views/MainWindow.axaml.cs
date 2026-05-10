using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Dragon.AppKit.Publisher.Core;

namespace Dragon.AppKit.Publisher.Views;

public partial class MainWindow : Window
{
    private readonly ProjectDiscovery _discovery = new();
    private readonly ProjectDetector _detector = new();
    private readonly PublishCommandBuilder _commandBuilder = new();
    private readonly VersionMetadataUpdater _versionUpdater = new();
    private readonly object _logFileLock = new();
    private readonly List<DragonProject> _projects = [];
    private DragonProject? _selectedProject;
    private bool _isLoading;
    private Process? _runningProcess;
    private string? _currentLogFile;

    public MainWindow()
    {
        InitializeComponent();
        InitializeControls();
        WorkspacePathBox.Text = FindDefaultWorkspaceRoot();
        ScanWorkspace();
    }

    private void InitializeControls()
    {
        ConfigurationBox.ItemsSource = new[] { "Release", "Debug" };
        ConfigurationBox.SelectedItem = "Release";
        AndroidPackageFormatBox.ItemsSource = new[] { "Both", "apk", "aab" };
        AndroidPackageFormatBox.SelectedItem = "Both";
        AndroidSigningModeBox.ItemsSource = new[] { "Unsigned", "Auto", "Keystore" };
        AndroidSigningModeBox.SelectedItem = "Unsigned";
        IosRuntimeBox.ItemsSource = new[] { "iossimulator-x64", "iossimulator-arm64", "ios-arm64" };
        IosRuntimeBox.SelectedItem = "iossimulator-x64";
        IosSigningBox.ItemsSource = new[] { "Unsigned", "Apple" };
        IosSigningBox.SelectedItem = "Unsigned";
        SelfContainedBox.IsChecked = true;
        IosArchiveBox.IsChecked = true;
        UpdateVersionBox.IsChecked = true;
        ButlerPathBox.Text = "butler";
        RunStatusText.Text = "Idle";
    }

    private async void BrowseWorkspace_Click(object? sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync("Select a workspace or project");
        if (folder is null)
        {
            return;
        }

        WorkspacePathBox.Text = folder;
        ScanWorkspace();
    }

    private async void BrowseArtifactOutput_Click(object? sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync("Select artifact output directory");
        if (folder is not null)
        {
            ArtifactOutputBox.Text = folder;
        }
    }

    private async void BrowseItchSource_Click(object? sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync("Select itch.io source directory");
        if (folder is not null)
        {
            ItchSourcePathBox.Text = folder;
        }
    }

    private async void BrowseButler_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select butler executable",
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file is not null)
        {
            ButlerPathBox.Text = file.Path.LocalPath;
        }
    }

    private void ScanWorkspace_Click(object? sender, RoutedEventArgs e)
    {
        ScanWorkspace();
    }

    private void ProjectList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProjectList.SelectedItem is DragonProject project)
        {
            LoadProject(project);
        }
    }

    private void Option_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        RefreshCommandPreviewIfReady();
    }

    private void Option_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshCommandPreviewIfReady();
    }

    private void Option_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        RefreshCommandPreviewIfReady();
    }

    private void Option_TextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshCommandPreviewIfReady();
    }

    private void RefreshPreview_Click(object? sender, RoutedEventArgs e)
    {
        RefreshCommandPreview();
    }

    private void ApplyVersion_Click(object? sender, RoutedEventArgs e)
    {
        ApplyVersionMetadata(ReadOptionsFromUi(), refreshProject: true);
    }

    private async void TestButler_Click(object? sender, RoutedEventArgs e)
    {
        var butlerPath = FirstNonBlank(ButlerPathBox.Text, "butler");
        AppendLog($"> & {Quote(butlerPath)} -V");
        var exitCode = await RunOneShotAsync($"& {Quote(butlerPath)} -V", _selectedProject?.RootPath ?? Environment.CurrentDirectory);
        AppendLog($"Butler probe exited with code {exitCode}.");
    }

    private async void RunPublish_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            AppendLog("Select a project before running publish.");
            return;
        }

        if (_runningProcess is not null)
        {
            AppendLog("A publish process is already running.");
            return;
        }

        var options = ReadOptionsFromUi();
        var commandText = CommandPreviewBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(commandText))
        {
            RefreshCommandPreview();
            commandText = CommandPreviewBox.Text?.Trim();
        }

        if (string.IsNullOrWhiteSpace(commandText))
        {
            AppendLog("Command preview is empty.");
            return;
        }

        OutputLogBox.Text = string.Empty;
        ProgressLogBox.Text = string.Empty;
        _currentLogFile = CreatePublisherLogFile(_selectedProject.RootPath);
        ShowProgress("Publishing", "Preparing release workflow.", commandText);
        AppendLog($"> {commandText}");
        AppendLog(string.Empty);

        RunButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        OverlayStopButton.IsEnabled = true;
        OverlayCloseButton.IsEnabled = false;

        try
        {
            if (options.UpdateVersionMetadata)
            {
                SetProgressDetail("Writing version metadata.");
                ApplyVersionMetadata(options, refreshProject: false);
            }

            SetProgressDetail("Running publish plan.");
            var exitCode = await RunPowerShellAsync(_selectedProject.RootPath, commandText);
            CompleteProgress(exitCode);
            if (options.UpdateVersionMetadata)
            {
                RefreshSelectedProject();
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Publish failed before process start: {ex.Message}");
            CompleteProgress(1);
        }
        finally
        {
            _runningProcess = null;
            RunButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            OverlayStopButton.IsEnabled = false;
            OverlayCloseButton.IsEnabled = true;
        }
    }

    private void StopPublish_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_runningProcess is { HasExited: false } process)
            {
                process.Kill(entireProcessTree: true);
                AppendLog("Publish process stopped.");
                SetProgressDetail("Stop requested.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Unable to stop process: {ex.Message}");
        }
    }

    private void DismissProgress_Click(object? sender, RoutedEventArgs e)
    {
        ProgressOverlay.IsVisible = false;
    }

    private void OpenProjectFolder_Click(object? sender, RoutedEventArgs e)
    {
        OpenFolder(_selectedProject?.RootPath);
    }

    private void OpenScriptsFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            return;
        }

        OpenFolder(Path.Combine(_selectedProject.RootPath, "scripts"));
    }

    private void OpenArtifactsFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            return;
        }

        OpenFolder(Path.Combine(_selectedProject.RootPath, "artifacts"));
    }

    private void OpenDocsFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            return;
        }

        OpenFolder(Path.Combine(_selectedProject.RootPath, "docs"));
    }

    private void OpenPublisherLogsFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedProject is null)
        {
            return;
        }

        OpenFolder(Path.Combine(_selectedProject.RootPath, "artifacts", "publisher-logs"));
    }

    private void ScanWorkspace()
    {
        var path = WorkspacePathBox.Text;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            AppendLog($"Workspace path does not exist: {path}");
            return;
        }

        _projects.Clear();

        try
        {
            if (LooksLikeProjectRoot(path))
            {
                _projects.Add(_detector.Detect(path));
            }
            else
            {
                _projects.AddRange(_discovery.Discover(path, maxDepth: 4));
            }

            ProjectList.ItemsSource = null;
            ProjectList.ItemsSource = _projects;
            AppendLog($"Discovered {_projects.Count} project(s).");

            if (_projects.Count > 0)
            {
                ProjectList.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Scan failed: {ex.Message}");
        }
    }

    private void LoadProject(DragonProject project)
    {
        _isLoading = true;
        _selectedProject = project;

        DetectedNameText.Text = project.DisplayName;
        DetectedPathText.Text = project.RootPath;
        DetectedTypeText.Text = project.ProjectKind;
        DetectedAppIdText.Text = string.IsNullOrWhiteSpace(project.AppId) ? "-" : project.AppId;
        DetectedVersionText.Text = string.IsNullOrWhiteSpace(project.Version) ? "-" : project.Version;
        DetectedTargetsText.Text = project.SupportedTargets.Count == 0 ? "-" : string.Join(", ", project.SupportedTargets);
        DetectedSolutionsText.Text = project.SolutionFiles.Count == 0 ? "-" : string.Join(", ", project.SolutionFiles);
        DetectedPublishText.Text = project.HasPublishScript ? project.PublishScriptRelativePath : "dotnet publish fallback";

        var options = PublishOptions.FromProject(project);
        ConfigurationBox.SelectedItem = options.Configuration;
        VersionBox.Text = options.Version;
        BuildVersionBox.Text = options.BuildVersion;
        SkipTestsBox.IsChecked = options.SkipTests;
        SelfContainedBox.IsChecked = options.SelfContainedDesktop;
        ArtifactOutputBox.Text = options.ArtifactOutputDirectory;
        UpdateVersionBox.IsChecked = options.UpdateVersionMetadata;
        AndroidVersionCodeBox.Value = options.AndroidVersionCode;
        AndroidPackageFormatBox.SelectedItem = options.AndroidPackageFormat;
        AndroidSigningModeBox.SelectedItem = options.AndroidSigningMode;
        IosRuntimeBox.SelectedItem = options.IosRuntimeIdentifier;
        IosSigningBox.SelectedItem = options.IosSigningMode;
        IosArchiveBox.IsChecked = options.IosArchiveOnBuild;
        ScreenshotQaBox.IsChecked = options.RunScreenshotQa;
        CleanSlateQaBox.IsChecked = options.RunCleanSlateQa;
        PerformanceQaBox.IsChecked = options.RunPerformanceQa;
        ExtraArgumentsBox.Text = options.ExtraArguments;
        ItchPublishBox.IsChecked = options.PublishToItch;
        ButlerPathBox.Text = options.ButlerPath;
        ItchUserBox.Text = options.ItchUser;
        ItchGameBox.Text = options.ItchGame;
        ItchChannelBox.Text = options.ItchChannel;
        ItchSourcePathBox.Text = options.ItchSourcePath;
        ItchExtraArgumentsBox.Text = options.ItchExtraArguments;

        SetPlatformChecks(options);

        _isLoading = false;
        RefreshCommandPreview();
    }

    private void SetPlatformChecks(PublishOptions options)
    {
        DesktopBox.IsChecked = options.Platforms.Contains("Desktop", StringComparer.OrdinalIgnoreCase);
        BrowserBox.IsChecked = options.Platforms.Contains("Browser", StringComparer.OrdinalIgnoreCase);
        AndroidBox.IsChecked = options.Platforms.Contains("Android", StringComparer.OrdinalIgnoreCase);
        IosBox.IsChecked = options.Platforms.Contains("iOS", StringComparer.OrdinalIgnoreCase);
        WindowsBox.IsChecked = options.Platforms.Contains("Windows", StringComparer.OrdinalIgnoreCase);
        LinuxBox.IsChecked = options.Platforms.Contains("Linux", StringComparer.OrdinalIgnoreCase);
        MacOsBox.IsChecked = options.Platforms.Contains("macOS", StringComparer.OrdinalIgnoreCase);

        WinX64Box.IsChecked = options.DesktopRuntimes.Contains("win-x64", StringComparer.OrdinalIgnoreCase);
        LinuxX64Box.IsChecked = options.DesktopRuntimes.Contains("linux-x64", StringComparer.OrdinalIgnoreCase);
        OsxX64Box.IsChecked = options.DesktopRuntimes.Contains("osx-x64", StringComparer.OrdinalIgnoreCase);
        OsxArm64Box.IsChecked = options.DesktopRuntimes.Contains("osx-arm64", StringComparer.OrdinalIgnoreCase);
    }

    private void RefreshCommandPreviewIfReady()
    {
        if (!_isLoading)
        {
            RefreshCommandPreview();
        }
    }

    private void RefreshCommandPreview()
    {
        if (_selectedProject is null)
        {
            CommandPreviewBox.Text = string.Empty;
            return;
        }

        try
        {
            var command = _commandBuilder.Build(_selectedProject, ReadOptionsFromUi());
            CommandPreviewBox.Text = command.DisplayText;
        }
        catch (Exception ex)
        {
            CommandPreviewBox.Text = string.Empty;
            AppendLog($"Could not build command preview: {ex.Message}");
        }
    }

    private PublishOptions ReadOptionsFromUi()
    {
        var options = new PublishOptions
        {
            Configuration = SelectedText(ConfigurationBox, "Release"),
            Version = VersionBox.Text?.Trim() ?? string.Empty,
            BuildVersion = BuildVersionBox.Text?.Trim() ?? string.Empty,
            SkipTests = SkipTestsBox.IsChecked == true,
            SelfContainedDesktop = SelfContainedBox.IsChecked == true,
            AndroidVersionCode = (int)(AndroidVersionCodeBox.Value ?? 0),
            AndroidPackageFormat = SelectedText(AndroidPackageFormatBox, "Both"),
            AndroidSigningMode = SelectedText(AndroidSigningModeBox, "Unsigned"),
            IosRuntimeIdentifier = SelectedText(IosRuntimeBox, "iossimulator-x64"),
            IosSigningMode = SelectedText(IosSigningBox, "Unsigned"),
            IosArchiveOnBuild = IosArchiveBox.IsChecked == true,
            RunScreenshotQa = ScreenshotQaBox.IsChecked == true,
            RunCleanSlateQa = CleanSlateQaBox.IsChecked == true,
            RunPerformanceQa = PerformanceQaBox.IsChecked == true,
            ArtifactOutputDirectory = ArtifactOutputBox.Text?.Trim() ?? string.Empty,
            ExtraArguments = ExtraArgumentsBox.Text?.Trim() ?? string.Empty,
            UpdateVersionMetadata = UpdateVersionBox.IsChecked == true,
            PublishToItch = ItchPublishBox.IsChecked == true,
            ButlerPath = FirstNonBlank(ButlerPathBox.Text, "butler"),
            ItchUser = ItchUserBox.Text?.Trim() ?? string.Empty,
            ItchGame = ItchGameBox.Text?.Trim() ?? string.Empty,
            ItchChannel = ItchChannelBox.Text?.Trim() ?? string.Empty,
            ItchSourcePath = ItchSourcePathBox.Text?.Trim() ?? string.Empty,
            ItchExtraArguments = ItchExtraArgumentsBox.Text?.Trim() ?? string.Empty
        };

        AddIfChecked(options.Platforms, DesktopBox, "Desktop");
        AddIfChecked(options.Platforms, BrowserBox, "Browser");
        AddIfChecked(options.Platforms, AndroidBox, "Android");
        AddIfChecked(options.Platforms, IosBox, "iOS");
        AddIfChecked(options.Platforms, WindowsBox, "Windows");
        AddIfChecked(options.Platforms, LinuxBox, "Linux");
        AddIfChecked(options.Platforms, MacOsBox, "macOS");
        AddIfChecked(options.DesktopRuntimes, WinX64Box, "win-x64");
        AddIfChecked(options.DesktopRuntimes, LinuxX64Box, "linux-x64");
        AddIfChecked(options.DesktopRuntimes, OsxX64Box, "osx-x64");
        AddIfChecked(options.DesktopRuntimes, OsxArm64Box, "osx-arm64");

        return options;
    }

    private VersionUpdateResult ApplyVersionMetadata(PublishOptions options, bool refreshProject)
    {
        if (_selectedProject is null)
        {
            return new VersionUpdateResult([]);
        }

        var result = _versionUpdater.Apply(_selectedProject, options);
        if (result.Changed)
        {
            AppendLog("Updated version metadata:");
            foreach (var file in result.UpdatedFiles)
            {
                AppendLog($"  {Path.GetRelativePath(_selectedProject.RootPath, file)}");
            }

            if (refreshProject)
            {
                RefreshSelectedProject();
            }
        }
        else
        {
            AppendLog("Version metadata is already current.");
        }

        return result;
    }

    private void RefreshSelectedProject()
    {
        if (_selectedProject is null)
        {
            return;
        }

        var root = _selectedProject.RootPath;
        var refreshed = _detector.Detect(root);
        var index = _projects.FindIndex(project => project.RootPath.Equals(root, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _projects[index] = refreshed;
            ProjectList.ItemsSource = null;
            ProjectList.ItemsSource = _projects;
            ProjectList.SelectedIndex = index;
        }

        LoadProject(refreshed);
    }

    private async Task<int> RunPowerShellAsync(string workingDirectory, string commandText)
    {
        return await RunOneShotAsync($"Set-Location -LiteralPath {Quote(workingDirectory)}; {commandText}", workingDirectory);
    }

    private async Task<int> RunOneShotAsync(string commandText, string workingDirectory)
    {
        var process = CreatePowerShellProcess("pwsh", commandText, workingDirectory);

        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            process = CreatePowerShellProcess("powershell", commandText, workingDirectory);
            process.Start();
        }

        _runningProcess = process;
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                AppendLog(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                AppendLog(args.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        AppendLog(string.Empty);
        AppendLog($"Process exited with code {process.ExitCode}.");
        if (ReferenceEquals(_runningProcess, process))
        {
            _runningProcess = null;
        }

        return process.ExitCode;
    }

    private static Process CreatePowerShellProcess(string executable, string command, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);

        return new Process { StartInfo = startInfo, EnableRaisingEvents = true };
    }

    private void ShowProgress(string title, string detail, string commandText)
    {
        ProgressOverlay.IsVisible = true;
        ProgressBar.IsIndeterminate = true;
        ProgressTitleText.Text = title;
        ProgressDetailText.Text = detail;
        ProgressExitText.Text = "Running";
        ProgressFooterText.Text = "Streaming output...";
        ProgressCommandText.Text = commandText;
        RunStatusText.Text = "Running";
    }

    private void SetProgressDetail(string detail)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressDetailText.Text = detail;
            RunStatusText.Text = detail;
        });
    }

    private void CompleteProgress(int exitCode)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = exitCode == 0 ? 100 : 0;
            ProgressExitText.Text = exitCode == 0 ? "Complete" : "Failed";
            ProgressTitleText.Text = exitCode == 0 ? "Publish Complete" : "Publish Failed";
            ProgressDetailText.Text = exitCode == 0 ? "The release workflow finished." : "The release workflow exited with errors.";
            ProgressFooterText.Text = $"Exit code {exitCode}";
            RunStatusText.Text = exitCode == 0 ? "Complete" : "Failed";
        });
    }

    private void AppendLog(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AppendToTextBox(OutputLogBox, message);
            if (ProgressOverlay.IsVisible)
            {
                AppendToTextBox(ProgressLogBox, message);
            }
        });

        if (!string.IsNullOrWhiteSpace(_currentLogFile))
        {
            try
            {
                lock (_logFileLock)
                {
                    File.AppendAllText(_currentLogFile, message + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never interrupt a release run.
            }
        }
    }

    private static void AppendToTextBox(TextBox textBox, string message)
    {
        var builder = new StringBuilder(textBox.Text ?? string.Empty);
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(message);
        textBox.Text = builder.ToString();
        textBox.CaretIndex = textBox.Text.Length;
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.Path.LocalPath;
    }

    private static bool LooksLikeProjectRoot(string path)
    {
        return File.Exists(Path.Combine(path, "dragon-app.json"))
            || Directory.GetFiles(path, "*.slnx", SearchOption.TopDirectoryOnly).Length > 0
            || Directory.GetFiles(path, "*.sln", SearchOption.TopDirectoryOnly).Length > 0
            || Directory.GetFiles(path, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0;
    }

    private static string CreatePublisherLogFile(string projectRoot)
    {
        var logRoot = Path.Combine(projectRoot, "artifacts", "publisher-logs");
        Directory.CreateDirectory(logRoot);
        return Path.Combine(logRoot, $"publisher-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }

    private void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var target = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target))
        {
            AppendLog($"Folder does not exist: {path}");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{target}\"",
            UseShellExecute = false
        });
    }

    private static void AddIfChecked(ICollection<string> values, CheckBox box, string value)
    {
        if (box.IsChecked == true)
        {
            values.Add(value);
        }
    }

    private static string SelectedText(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem?.ToString() ?? fallback;
    }

    private static string FirstNonBlank(params string?[] values)
    {
        return values.First(value => !string.IsNullOrWhiteSpace(value))!.Trim();
    }

    private static string Quote(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }

    private static string FindDefaultWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dragon.AppKit.slnx")))
            {
                return directory.Parent?.FullName ?? directory.FullName;
            }

            directory = directory.Parent;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
