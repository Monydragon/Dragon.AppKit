using Avalonia;
using DragonTemplateApp = DragonTemplate.App.App;

namespace DragonTemplate.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<DragonTemplateApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}

