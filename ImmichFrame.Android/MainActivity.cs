using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using AppSettings = ImmichFrame.Models.Settings;

namespace ImmichFrame.Android;

[Activity(
    Label = "ImmichFrame Standalone",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/AppIcon",
    Name = "io.github.tastelessjolt.immichframestandalone.MainActivity",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.KeepScreenOn);
        Window!.AddFlags(WindowManagerFlags.Fullscreen);
        AppSettings.SettingsSaved += HandleSettingsSaved;
        ScreenScheduleScheduler.Schedule(this, applyCurrentState: true);
    }

    protected override void OnResume()
    {
        base.OnResume();
        ScreenPowerController.ReleaseWakeLock();
    }

    protected override void OnDestroy()
    {
        AppSettings.SettingsSaved -= HandleSettingsSaved;
        base.OnDestroy();
    }

    private void HandleSettingsSaved() =>
        ScreenScheduleScheduler.Schedule(this, applyCurrentState: true);

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder);
    }
}
