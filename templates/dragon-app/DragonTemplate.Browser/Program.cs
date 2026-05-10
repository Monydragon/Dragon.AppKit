using Avalonia;
using Avalonia.Browser;
using DragonTemplateApp = DragonTemplate.App.App;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        return BuildAvaloniaApp()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<DragonTemplateApp>();
    }
}

