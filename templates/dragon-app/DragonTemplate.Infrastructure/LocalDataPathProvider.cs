using DragonTemplate.Application;

namespace DragonTemplate.Infrastructure;

public sealed class LocalDataPathProvider : ILocalDataPathProvider
{
    public LocalDataPathProvider()
    {
        var configuredDatabasePath = Environment.GetEnvironmentVariable("DRAGON_TEMPLATE_DATABASE_PATH");
        if (!string.IsNullOrWhiteSpace(configuredDatabasePath))
        {
            DatabasePath = Path.GetFullPath(configuredDatabasePath);
            LocalDataDirectory = Path.GetDirectoryName(DatabasePath) ?? Path.GetTempPath();
            return;
        }

        var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localRoot))
        {
            localRoot = Path.GetTempPath();
        }

        LocalDataDirectory = Path.Combine(localRoot, "DragonTemplate");
        DatabasePath = Path.Combine(LocalDataDirectory, "dragontemplate.db");
    }

    public string LocalDataDirectory { get; }

    public string DatabasePath { get; }
}

