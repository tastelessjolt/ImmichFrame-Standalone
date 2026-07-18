using Avalonia.Threading;
using System;

namespace ImmichFrame.Helpers;

/// <summary>
/// Defers disposal of resources that have just been detached from Avalonia
/// bindings. Layout and transitions may continue to reference the old value
/// briefly after the property-change notification has completed.
/// </summary>
public static class UiResourceLifetime
{
    private static readonly TimeSpan TransitionSafetyMargin = TimeSpan.FromSeconds(1);

    public static TimeSpan AfterTransition(double transitionSeconds)
    {
        return TimeSpan.FromSeconds(Math.Max(0, transitionSeconds)) + TransitionSafetyMargin;
    }

    public static void DisposeAfter(IDisposable? resource, TimeSpan delay)
    {
        if (resource == null)
            return;

        void Schedule()
        {
            DispatcherTimer.RunOnce(
                () => DisposeSafely(resource),
                delay < TimeSpan.Zero ? TimeSpan.Zero : delay,
                DispatcherPriority.Background);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Schedule();
        else
            Dispatcher.UIThread.Post(Schedule, DispatcherPriority.Background);
    }

    private static void DisposeSafely(IDisposable resource)
    {
        try
        {
            resource.Dispose();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ImmichFrame deferred UI resource disposal failed: {ex}");
        }
    }
}
