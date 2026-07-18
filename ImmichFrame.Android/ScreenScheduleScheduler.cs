using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Helpers;
using AppSettings = ImmichFrame.Models.Settings;
using System;
using System.Globalization;
using System.IO;

namespace ImmichFrame.Android;

internal static class ScreenScheduleScheduler
{
    public const string SleepAction = "io.github.tastelessjolt.immichframestandalone.action.SLEEP";
    public const string WakeAction = "io.github.tastelessjolt.immichframestandalone.action.WAKE";

    private const string LogTag = "ImmichFramePower";
    private const int SleepRequestCode = 4101;
    private const int WakeRequestCode = 4102;

    public static SunriseScreenSchedulePlan? Schedule(Context context, bool applyCurrentState)
    {
        var applicationContext = context.ApplicationContext ?? context;

        try
        {
            if (!File.Exists(AppSettings.JsonSettingsPath))
            {
                Cancel(applicationContext);
                return null;
            }

            var settings = AppSettings.CurrentSettings;
            if (!settings.SunriseScreenScheduleEnabled)
            {
                Cancel(applicationContext);
                Log.Info(LogTag, "Sunrise screen schedule is disabled");
                return null;
            }

            if (!TryParseCoordinates(settings.WeatherLatLong, out var latitude, out var longitude))
                throw new InvalidDataException("Weather coordinates are unavailable or invalid");

            var plan = SunriseScreenSchedule.CreatePlan(
                DateTimeOffset.Now,
                latitude,
                longitude,
                TimeZoneInfo.Local);
            if (plan == null)
                throw new InvalidOperationException("No sunrise could be calculated for the configured location");

            CancelLegacyBroadcastAlarms(applicationContext);
            SetAlarm(applicationContext, SleepAction, SleepRequestCode, plan.NextSleep);
            SetAlarm(applicationContext, WakeAction, WakeRequestCode, plan.NextWake);

            Log.Info(
                LogTag,
                $"Scheduled sleep {Format(plan.NextSleep)}, wake {Format(plan.NextWake)}, "
                + $"sunset {Format(plan.Today.Sunset)}");

            if (applyCurrentState && plan.ShouldBeAsleep)
                ScreenPowerController.Sleep(applicationContext);

            return plan;
        }
        catch (SettingsNotValidException ex)
        {
            Cancel(applicationContext);
            Log.Warn(LogTag, $"Screen schedule not applied because settings are invalid: {ex.Message}");
        }
        catch (Exception ex)
        {
            Cancel(applicationContext);
            Log.Error(LogTag, $"Screen schedule failed: {ex}");
        }

        return null;
    }

    public static void Cancel(Context context)
    {
        var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
        if (alarmManager == null)
            return;

        alarmManager.Cancel(CreatePendingIntent(context, SleepAction, SleepRequestCode));
        alarmManager.Cancel(CreatePendingIntent(context, WakeAction, WakeRequestCode));
        CancelLegacyBroadcastAlarms(context);
    }

    private static void SetAlarm(Context context, string action, int requestCode, DateTimeOffset trigger)
    {
        var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager
            ?? throw new InvalidOperationException("Android AlarmManager is unavailable");
        var pendingIntent = CreatePendingIntent(context, action, requestCode);
        var triggerMilliseconds = trigger.ToUnixTimeMilliseconds();

        if (OperatingSystem.IsAndroidVersionAtLeast(31)
            && !alarmManager.CanScheduleExactAlarms())
        {
            alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMilliseconds, pendingIntent);
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMilliseconds, pendingIntent);
        }
        else
        {
            alarmManager.SetExact(AlarmType.RtcWakeup, triggerMilliseconds, pendingIntent);
        }
    }

    private static PendingIntent CreatePendingIntent(Context context, string action, int requestCode)
    {
        var intent = new Intent(action);
        intent.SetClassName(
            context.PackageName!,
            "io.github.tastelessjolt.immichframestandalone.ScreenScheduleBootstrapActivity");
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.NoAnimation | ActivityFlags.ExcludeFromRecents);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            flags |= PendingIntentFlags.Immutable;

        return PendingIntent.GetActivity(context, requestCode, intent, flags)
            ?? throw new InvalidOperationException("Could not create the screen schedule alarm");
    }

    private static void CancelLegacyBroadcastAlarms(Context context)
    {
        var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
        if (alarmManager == null)
            return;

        alarmManager.Cancel(CreateLegacyBroadcastPendingIntent(context, SleepAction, SleepRequestCode));
        alarmManager.Cancel(CreateLegacyBroadcastPendingIntent(context, WakeAction, WakeRequestCode));
    }

    private static PendingIntent CreateLegacyBroadcastPendingIntent(Context context, string action, int requestCode)
    {
        var intent = new Intent(action);
        intent.SetClassName(
            context.PackageName!,
            "io.github.tastelessjolt.immichframestandalone.ScreenScheduleReceiver");

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            flags |= PendingIntentFlags.Immutable;

        return PendingIntent.GetBroadcast(context, requestCode, intent, flags)
            ?? throw new InvalidOperationException("Could not access the legacy screen schedule alarm");
    }

    private static bool TryParseCoordinates(string? value, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        var parts = value?.Split(',');
        return parts?.Length == 2
            && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
            && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
            && latitude is >= -90d and <= 90d
            && longitude is >= -180d and <= 180d;
    }

    private static string Format(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? "unavailable";
}

internal static class ScreenPowerController
{
    private const string LogTag = "ImmichFramePower";
    private const int SleepKeyCode = 223;
    private const int WakeKeyCode = 224;
    private static readonly object WakeLockSync = new();
    private static PowerManager.WakeLock? wakeLock;

    public static void Sleep(Context context)
    {
        var powerManager = context.GetSystemService(Context.PowerService) as PowerManager;
        if (powerManager?.IsInteractive == false)
            return;

        ExecuteRootKeyEvent(SleepKeyCode, "sleep");
    }

    public static void Wake(Context context)
    {
        var powerManager = context.GetSystemService(Context.PowerService) as PowerManager;
        if (powerManager == null)
            return;

        if (!powerManager.IsInteractive)
        {
            lock (WakeLockSync)
            {
                ReleaseWakeLockLocked();
                wakeLock = powerManager.NewWakeLock(
                    WakeLockFlags.ScreenBright | WakeLockFlags.AcquireCausesWakeup | WakeLockFlags.OnAfterRelease,
                    "ImmichFrame::SunriseWakeLock");
                wakeLock?.Acquire(30_000);
            }

            ExecuteRootKeyEvent(WakeKeyCode, "wake");
        }

        var activityIntent = new Intent(context, typeof(MainActivity));
        activityIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ReorderToFront | ActivityFlags.SingleTop);
        context.StartActivity(activityIntent);
    }

    public static void ReleaseWakeLock()
    {
        lock (WakeLockSync)
            ReleaseWakeLockLocked();
    }

    private static void ReleaseWakeLockLocked()
    {
        if (wakeLock?.IsHeld == true)
            wakeLock.Release();
        wakeLock?.Dispose();
        wakeLock = null;
    }

    private static void ExecuteRootKeyEvent(int keyCode, string operation)
    {
        try
        {
            using var process = Java.Lang.Runtime.GetRuntime()?.Exec(
                new[] { "su", "-c", $"input keyevent {keyCode}" });
            var exitCode = process?.WaitFor() ?? -1;
            if (exitCode == 0)
                Log.Info(LogTag, $"Screen {operation} command completed");
            else
                Log.Error(LogTag, $"Screen {operation} command failed with exit code {exitCode}");
        }
        catch (Exception ex)
        {
            Log.Error(LogTag, $"Screen {operation} command failed: {ex}");
        }
    }
}
