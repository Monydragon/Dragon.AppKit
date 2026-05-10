using System.Reflection;
using DragonTemplate.Application;

namespace DragonTemplate.Infrastructure;

public sealed class AppInfoProvider : IAppInfoProvider
{
    public AppInfo Current { get; } = new(
        "Dragon Template",
        typeof(AppInfoProvider).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(AppInfoProvider).Assembly.GetName().Version?.ToString()
            ?? "0.1.0-local",
        "com.dragontemplate.app",
        "DRAGON_TEMPLATE");
}

