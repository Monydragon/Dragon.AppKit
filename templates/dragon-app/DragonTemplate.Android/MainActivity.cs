using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace DragonTemplate.Android;

[Activity(
    Label = "Dragon Template",
    Theme = "@style/AppTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation
                           | ConfigChanges.ScreenSize
                           | ConfigChanges.UiMode
                           | ConfigChanges.KeyboardHidden)]
public sealed class MainActivity : AvaloniaMainActivity
{
}

