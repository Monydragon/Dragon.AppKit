using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Dragon.AppKit.Publisher.Core;

public sealed class VersionMetadataUpdater
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public VersionUpdateResult Apply(DragonProject project, PublishOptions options)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);

        var updatedFiles = new List<string>();
        var version = options.Version.Trim();
        var androidVersionCode = options.AndroidVersionCode;

        if (string.IsNullOrWhiteSpace(version) && androidVersionCode <= 0)
        {
            return new VersionUpdateResult(updatedFiles);
        }

        UpdateDragonAppContract(project, version, androidVersionCode, updatedFiles);

        var directoryBuildProps = Path.Combine(project.RootPath, "Directory.Build.props");
        var hasDirectoryBuildProps = File.Exists(directoryBuildProps);
        if (hasDirectoryBuildProps && UpdateXmlVersionFile(directoryBuildProps, version, androidVersionCode, forceVersionProperties: true, isAndroidProject: false))
        {
            updatedFiles.Add(directoryBuildProps);
        }

        foreach (var projectFile in project.ProjectFiles)
        {
            var shouldForceVersion = !hasDirectoryBuildProps;
            var isAndroid = projectFile.IsAndroid;
            if (UpdateXmlVersionFile(projectFile.Path, version, androidVersionCode, shouldForceVersion, isAndroid))
            {
                updatedFiles.Add(projectFile.Path);
            }
        }

        return new VersionUpdateResult(updatedFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void UpdateDragonAppContract(DragonProject project, string version, int androidVersionCode, List<string> updatedFiles)
    {
        var path = Path.Combine(project.RootPath, "dragon-app.json");
        if (!File.Exists(path))
        {
            return;
        }

        var json = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
        if (json is null)
        {
            return;
        }

        var changed = false;
        if (!string.IsNullOrWhiteSpace(version) && json["version"]?.GetValue<string>() != version)
        {
            json["version"] = version;
            changed = true;
        }

        var existingAndroidVersionCode = json["androidVersionCode"] is JsonValue value && value.TryGetValue<int>(out var currentCode)
            ? currentCode
            : 0;
        if (androidVersionCode > 0 && existingAndroidVersionCode != androidVersionCode)
        {
            json["androidVersionCode"] = androidVersionCode;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        File.WriteAllText(path, json.ToJsonString(JsonOptions) + Environment.NewLine);
        updatedFiles.Add(path);
    }

    private static bool UpdateXmlVersionFile(string path, string version, int androidVersionCode, bool forceVersionProperties, bool isAndroidProject)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var propertyGroup = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "PropertyGroup");
        if (document.Root is null || propertyGroup is null)
        {
            return false;
        }

        var changed = false;
        if (!string.IsNullOrWhiteSpace(version))
        {
            changed |= SetProperty(propertyGroup, "Version", version, forceVersionProperties);
            changed |= SetProperty(propertyGroup, "PackageVersion", version, required: false);
            changed |= SetProperty(propertyGroup, "InformationalVersion", $"{version}-local", forceVersionProperties);
            changed |= SetProperty(propertyGroup, "AssemblyVersion", ToAssemblyVersion(version), forceVersionProperties);
            changed |= SetProperty(propertyGroup, "FileVersion", ToAssemblyVersion(version), forceVersionProperties);

            if (isAndroidProject)
            {
                changed |= SetProperty(propertyGroup, "ApplicationDisplayVersion", version, required: true);
            }
        }

        if (isAndroidProject && androidVersionCode > 0)
        {
            changed |= SetProperty(propertyGroup, "ApplicationVersion", androidVersionCode.ToString(CultureInfo.InvariantCulture), required: true);
        }

        if (!changed)
        {
            return false;
        }

        document.Save(path);
        return true;
    }

    private static bool SetProperty(XElement propertyGroup, string name, string value, bool required)
    {
        var element = propertyGroup.Elements().FirstOrDefault(element => element.Name.LocalName == name);
        if (element is null)
        {
            if (!required)
            {
                return false;
            }

            propertyGroup.Add(new XElement(name, value));
            return true;
        }

        if (element.Value == value)
        {
            return false;
        }

        element.Value = value;
        return true;
    }

    private static string ToAssemblyVersion(string version)
    {
        var core = version.Split('-', '+')[0];
        var parts = core.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var number) ? number : 0)
            .Take(4)
            .ToList();

        while (parts.Count < 4)
        {
            parts.Add(0);
        }

        return string.Join('.', parts);
    }
}
