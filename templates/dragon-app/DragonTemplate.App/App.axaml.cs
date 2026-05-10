using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DragonTemplate.App.Views;

namespace DragonTemplate.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            ApplyStartupSize(window);
            desktop.MainWindow = window;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyStartupSize(MainWindow window)
    {
        if (TryReadDouble("DRAGON_TEMPLATE_VIEWPORT_WIDTH", out var width))
        {
            window.Width = width;
        }

        if (TryReadDouble("DRAGON_TEMPLATE_VIEWPORT_HEIGHT", out var height))
        {
            window.Height = height;
        }
    }

    private static bool TryReadDouble(string name, out double value)
    {
        return double.TryParse(
            Environment.GetEnvironmentVariable(name),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }
}

