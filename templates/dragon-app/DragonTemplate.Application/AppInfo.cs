namespace DragonTemplate.Application;

public sealed record AppInfo(
    string ProductName,
    string Version,
    string AppId,
    string EnvPrefix);

public interface IAppInfoProvider
{
    AppInfo Current { get; }
}

public interface ILocalDataPathProvider
{
    string LocalDataDirectory { get; }

    string DatabasePath { get; }
}

