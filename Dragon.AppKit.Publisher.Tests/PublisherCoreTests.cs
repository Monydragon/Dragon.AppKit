using Dragon.AppKit.Publisher.Core;
using Xunit;

namespace Dragon.AppKit.Publisher.Tests;

public sealed class PublisherCoreTests
{
    [Fact]
    public void Detector_ReadsDragonAppContractAndPublishParameters()
    {
        var repo = CreateRepo();
        File.WriteAllText(Path.Combine(repo, "dragon-app.json"), """
        {
          "productName": "Dragon Example",
          "shortName": "DragonExample",
          "namespace": "DragonExample",
          "appId": "com.dragonexample.app",
          "envPrefix": "DRAGON_EXAMPLE",
          "version": "0.2.0",
          "androidVersionCode": 20,
          "supportedTargets": ["Desktop", "Browser", "Android"],
          "release": {
            "defaultPlatforms": ["Desktop", "Browser"],
            "defaultDesktopRuntimes": ["win-x64"]
          }
        }
        """);
        Directory.CreateDirectory(Path.Combine(repo, "scripts"));
        File.WriteAllText(Path.Combine(repo, "scripts", "publish-releases.ps1"), """
        param(
          [string]$Version,
          [string[]]$Platform,
          [switch]$SkipTests
        )
        """);

        var project = new ProjectDetector().Detect(repo);

        Assert.Equal("Dragon Example", project.DisplayName);
        Assert.Equal("Dragon App", project.ProjectKind);
        Assert.Contains("Browser", project.SupportedTargets);
        Assert.Contains("Version", project.ScriptParameters);
        Assert.Contains("SkipTests", project.ScriptParameters);
    }

    [Fact]
    public void CommandBuilder_OnlyEmitsParametersSupportedBySelectedScript()
    {
        var repo = CreateRepo();
        File.WriteAllText(Path.Combine(repo, "dragon-app.json"), """
        {
          "productName": "Dragon Example",
          "shortName": "DragonExample",
          "namespace": "DragonExample",
          "appId": "com.dragonexample.app",
          "envPrefix": "DRAGON_EXAMPLE",
          "version": "0.2.0",
          "androidVersionCode": 20,
          "supportedTargets": ["Desktop", "Browser", "Android"]
        }
        """);
        Directory.CreateDirectory(Path.Combine(repo, "scripts"));
        File.WriteAllText(Path.Combine(repo, "scripts", "publish-releases.ps1"), """
        param(
          [string]$Version,
          [string[]]$Platform,
          [string]$AndroidSigningMode,
          [switch]$SkipTests
        )
        """);

        var project = new ProjectDetector().Detect(repo);
        var options = new PublishOptions
        {
            Version = "0.2.0",
            AndroidSigningMode = "Unsigned",
            AndroidPackageFormat = "Both",
            SkipTests = true
        };
        options.Platforms.AddRange(["Desktop", "Browser"]);

        var command = new PublishCommandBuilder().Build(project, options).DisplayText;

        Assert.Contains("-Version '0.2.0'", command);
        Assert.Contains("-Platform Desktop,Browser", command);
        Assert.Contains("-AndroidSigningMode 'Unsigned'", command);
        Assert.Contains("-SkipTests", command);
        Assert.DoesNotContain("AndroidPackageFormat", command);
    }

    [Fact]
    public void Detector_FindsKniStylePlatformProjectsWithoutDragonContract()
    {
        var repo = CreateRepo();
        File.WriteAllText(Path.Combine(repo, "Game.Windows.csproj"), """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Kni.Framework" Version="4.0.0" />
          </ItemGroup>
        </Project>
        """);

        var project = new ProjectDetector().Detect(repo);

        Assert.Equal("KNI/MonoGame App", project.ProjectKind);
        Assert.Contains("Windows", project.SupportedTargets);
        Assert.Contains(project.ProjectFiles, file => file.IsKniOrMonoGame);
    }

    [Fact]
    public void Discovery_FindsDragonAndGenericProjectRoots()
    {
        var workspace = CreateRepo();
        var dragonRepo = Path.Combine(workspace, "Dragon Example");
        var genericRepo = Path.Combine(workspace, "Kni Example");
        Directory.CreateDirectory(dragonRepo);
        Directory.CreateDirectory(genericRepo);
        File.WriteAllText(Path.Combine(dragonRepo, "dragon-app.json"), """
        {
          "productName": "Dragon Example",
          "shortName": "DragonExample",
          "namespace": "DragonExample",
          "appId": "com.dragonexample.app",
          "envPrefix": "DRAGON_EXAMPLE",
          "version": "0.2.0",
          "androidVersionCode": 20,
          "supportedTargets": ["Desktop"]
        }
        """);
        File.WriteAllText(Path.Combine(genericRepo, "KniExample.slnx"), "<Solution></Solution>");

        var projects = new ProjectDiscovery().Discover(workspace);

        Assert.Contains(projects, project => project.DisplayName == "Dragon Example");
        Assert.Contains(projects, project => project.DisplayName == "Kni Example");
    }


    [Fact]
    public void VersionUpdater_WritesDragonContractPropsAndAndroidHead()
    {
        var repo = CreateRepo();
        File.WriteAllText(Path.Combine(repo, "dragon-app.json"), """
        {
          "productName": "Dragon Example",
          "shortName": "DragonExample",
          "namespace": "DragonExample",
          "appId": "com.dragonexample.app",
          "envPrefix": "DRAGON_EXAMPLE",
          "version": "0.2.0",
          "androidVersionCode": 20,
          "supportedTargets": ["Desktop", "Android"]
        }
        """);
        File.WriteAllText(Path.Combine(repo, "Directory.Build.props"), """
        <Project>
          <PropertyGroup>
            <Version>0.2.0</Version>
            <InformationalVersion>0.2.0-local</InformationalVersion>
            <AssemblyVersion>0.2.0.0</AssemblyVersion>
            <FileVersion>0.2.0.0</FileVersion>
          </PropertyGroup>
        </Project>
        """);
        Directory.CreateDirectory(Path.Combine(repo, "DragonExample.Android"));
        File.WriteAllText(Path.Combine(repo, "DragonExample.Android", "DragonExample.Android.csproj"), """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0-android</TargetFramework>
          </PropertyGroup>
        </Project>
        """);

        var project = new ProjectDetector().Detect(repo);
        var result = new VersionMetadataUpdater().Apply(project, new PublishOptions
        {
            Version = "0.3.0-beta.1",
            AndroidVersionCode = 31
        });

        Assert.True(result.Changed);
        Assert.Contains("\"version\": \"0.3.0-beta.1\"", File.ReadAllText(Path.Combine(repo, "dragon-app.json")));
        Assert.Contains("<Version>0.3.0-beta.1</Version>", File.ReadAllText(Path.Combine(repo, "Directory.Build.props")));
        var androidProject = File.ReadAllText(Path.Combine(repo, "DragonExample.Android", "DragonExample.Android.csproj"));
        Assert.Contains("<ApplicationDisplayVersion>0.3.0-beta.1</ApplicationDisplayVersion>", androidProject);
        Assert.Contains("<ApplicationVersion>31</ApplicationVersion>", androidProject);
    }

    [Fact]
    public void CommandBuilder_AppendsButlerPushWhenItchPublishingIsEnabled()
    {
        var repo = CreateRepo();
        File.WriteAllText(Path.Combine(repo, "dragon-app.json"), """
        {
          "productName": "Dragon Example",
          "shortName": "DragonExample",
          "namespace": "DragonExample",
          "appId": "com.dragonexample.app",
          "envPrefix": "DRAGON_EXAMPLE",
          "version": "0.2.0",
          "androidVersionCode": 20,
          "supportedTargets": ["Desktop"]
        }
        """);
        Directory.CreateDirectory(Path.Combine(repo, "scripts"));
        File.WriteAllText(Path.Combine(repo, "scripts", "publish-releases.ps1"), """
        param(
          [string]$Version
        )
        """);

        var project = new ProjectDetector().Detect(repo);
        var options = new PublishOptions
        {
            Version = "0.2.0",
            PublishToItch = true,
            ButlerPath = "butler",
            ItchUser = "dragon",
            ItchGame = "example",
            ItchChannel = "windows"
        };

        var command = new PublishCommandBuilder().Build(project, options).DisplayText;

        Assert.Contains(".\\scripts\\publish-releases.ps1", command);
        Assert.Contains("& 'butler' push", command);
        Assert.Contains("'dragon/example:windows'", command);
    }

    [Fact]
    public void CommandBuilder_MapsArtifactOutputDirectoryToReleaseRootWhenScriptSupportsIt()
    {
        var repo = CreateRepo();
        File.WriteAllText(Path.Combine(repo, "dragon-app.json"), """
        {
          "productName": "Dragon Example",
          "shortName": "DragonExample",
          "namespace": "DragonExample",
          "appId": "com.dragonexample.app",
          "envPrefix": "DRAGON_EXAMPLE",
          "version": "0.1.0",
          "androidVersionCode": 10,
          "supportedTargets": ["Desktop"]
        }
        """);
        Directory.CreateDirectory(Path.Combine(repo, "scripts"));
        File.WriteAllText(Path.Combine(repo, "scripts", "publish-releases.ps1"), """
        param(
          [string]$Version,
          [string]$ReleaseRoot
        )
        """);

        var project = new ProjectDetector().Detect(repo);
        var options = new PublishOptions
        {
            Version = "0.1.0",
            ArtifactOutputDirectory = "artifacts\\releases\\v0.1.0"
        };

        var command = new PublishCommandBuilder().Build(project, options).DisplayText;

        Assert.Contains("-ReleaseRoot 'artifacts\\releases\\v0.1.0'", command);
        Assert.DoesNotContain("ArtifactOutputDirectory", command);
    }

    [Fact]
    public void ReleaseVersionScanner_VerifiesLatestReleaseFolderAndManifest()
    {
        var repo = CreateRepo();
        File.WriteAllText(Path.Combine(repo, "dragon-app.json"), """
        {
          "productName": "Dragon Example",
          "shortName": "DragonExample",
          "namespace": "DragonExample",
          "appId": "com.dragonexample.app",
          "envPrefix": "DRAGON_EXAMPLE",
          "version": "0.1.0",
          "androidVersionCode": 10,
          "supportedTargets": ["Desktop"]
        }
        """);
        var oldRelease = Path.Combine(repo, "artifacts", "releases", "v0.0.9");
        var latestRelease = Path.Combine(repo, "artifacts", "releases", "v0.1.0");
        Directory.CreateDirectory(oldRelease);
        Directory.CreateDirectory(latestRelease);
        File.WriteAllText(Path.Combine(latestRelease, "release-manifest.json"), """
        {
          "version": "0.1.0",
          "tag": "v0.1.0"
        }
        """);
        Directory.SetLastWriteTimeUtc(oldRelease, DateTime.UtcNow.AddHours(-2));
        Directory.SetLastWriteTimeUtc(latestRelease, DateTime.UtcNow);

        var project = new ProjectDetector().Detect(repo);
        var result = new ReleaseVersionScanner().ScanLatest(project, PublishOptions.FromProject(project));

        Assert.Equal(ReleaseVersionScanStatus.Consistent, result.Status);
        Assert.False(result.BlocksPublish);
        Assert.EndsWith("v0.1.0", result.ScannedReleasePath);
    }

    [Fact]
    public void ReleaseVersionScanner_FlagsLatestReleaseVersionMismatch()
    {
        var repo = CreateRepo();
        File.WriteAllText(Path.Combine(repo, "dragon-app.json"), """
        {
          "productName": "Dragon Example",
          "shortName": "DragonExample",
          "namespace": "DragonExample",
          "appId": "com.dragonexample.app",
          "envPrefix": "DRAGON_EXAMPLE",
          "version": "0.1.0",
          "androidVersionCode": 10,
          "supportedTargets": ["Desktop"]
        }
        """);
        var latestRelease = Path.Combine(repo, "artifacts", "releases", "v0.2.0");
        Directory.CreateDirectory(latestRelease);
        File.WriteAllText(Path.Combine(latestRelease, "release-manifest.json"), """
        {
          "version": "0.2.0",
          "tag": "v0.2.0"
        }
        """);

        var project = new ProjectDetector().Detect(repo);
        var result = new ReleaseVersionScanner().ScanLatest(project, PublishOptions.FromProject(project));

        Assert.Equal(ReleaseVersionScanStatus.Mismatch, result.Status);
        Assert.True(result.BlocksPublish);
        Assert.Contains("Expected 0.1.0", result.Message);
    }

    private static string CreateRepo()
    {
        var path = Path.Combine(Path.GetTempPath(), "DragonPublisherTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
