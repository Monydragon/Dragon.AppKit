using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using DragonTemplateApp = DragonTemplate.App.App;

namespace DragonTemplate.Android;

[Application]
public sealed class AndroidApp : AvaloniaAndroidApplication<DragonTemplateApp>
{
    public AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}

