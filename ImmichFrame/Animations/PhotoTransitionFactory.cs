using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using ImmichFrame.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImmichFrame.Animations;

public static class PhotoTransitionFactory
{
    public static IPageTransition Create(
        string? name,
        TimeSpan duration,
        Border? transitionShade = null)
    {
        duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        var normalizedName = TransitionAnimationOptions.All.Contains(name)
            ? name!
            : TransitionAnimationOptions.Crossfade;

        return new FrameDrivenPhotoTransition(
            normalizedName,
            duration,
            transitionShade);
    }

    internal static SplineEasing Curve(double x1, double y1, double x2, double y2)
    {
        // Avalonia 11.2.0-beta1's four-argument SplineEasing constructor does
        // not assign Y2 correctly, so use explicit properties.
        return new SplineEasing
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
        };
    }
}

/// <summary>
/// A page transition which advances ordinary Avalonia properties once per
/// rendered frame. This intentionally avoids Avalonia's Animation.RunAsync
/// path: on the Frameo's Android 6 renderer those animations completed on
/// schedule, but their intermediate compositor frames were never displayed.
/// </summary>
public sealed class FrameDrivenPhotoTransition : IPageTransition
{
    private readonly SplineEasing easing;
    private readonly Border? transitionShade;

    internal FrameDrivenPhotoTransition(
        string name,
        TimeSpan duration,
        Border? transitionShade)
    {
        Name = name;
        Duration = duration;
        this.transitionShade = transitionShade;
        easing = name switch
        {
            TransitionAnimationOptions.CleanPush => PhotoTransitionFactory.Curve(.65, 0, .35, 1),
            TransitionAnimationOptions.EdgeWipe => PhotoTransitionFactory.Curve(.76, 0, .24, 1),
            TransitionAnimationOptions.SoftIris => PhotoTransitionFactory.Curve(.22, 1, .36, 1),
            TransitionAnimationOptions.QuietCover => PhotoTransitionFactory.Curve(.22, 1, .36, 1),
            TransitionAnimationOptions.ExposureDip => PhotoTransitionFactory.Curve(.45, 0, .2, 1),
            _ => PhotoTransitionFactory.Curve(.4, 0, .2, 1),
        };
    }

    public string Name { get; }
    public TimeSpan Duration { get; }

    public async Task Start(
        Visual? from,
        Visual? to,
        bool forward,
        CancellationToken cancellationToken)
    {
        if (to is null || cancellationToken.IsCancellationRequested)
            return;

        if (from is null || Duration <= TimeSpan.Zero)
        {
            to.IsVisible = true;
            to.Opacity = 1;
            if (from is not null)
                from.IsVisible = false;
            return;
        }

        var fromState = VisualState.Capture(from);
        var toState = VisualState.Capture(to);
        var fromTranslation = new TranslateTransform();
        var toTranslation = new TranslateTransform();
        RectangleGeometry? wipeClip = null;
        EllipseGeometry? irisClip = null;
        var originalShadeBackground = transitionShade?.Background;
        var shadeBrush = transitionShade is not null
            ? new SolidColorBrush(Colors.Transparent)
            : null;
        var completed = false;

        try
        {
            from.IsVisible = true;
            from.Opacity = 1;
            from.ZIndex = 1;
            to.IsVisible = true;
            to.Opacity = 1;
            to.ZIndex = 0;

            // Render the decoded incoming photo underneath the current photo
            // for two frames. This pays the texture upload cost before the
            // timed motion starts, without flashing the next photo.
            Invalidate(from, to);
            await WaitForNextFrameAsync(to, cancellationToken);
            await WaitForNextFrameAsync(to, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var size = TransitionSize(to);
            var direction = forward ? 1d : -1d;

            switch (Name)
            {
                case TransitionAnimationOptions.CleanPush:
                    from.RenderTransform = fromTranslation;
                    to.RenderTransform = toTranslation;
                    break;
                case TransitionAnimationOptions.EdgeWipe:
                    wipeClip = new RectangleGeometry();
                    to.Clip = wipeClip;
                    break;
                case TransitionAnimationOptions.SoftIris:
                    irisClip = new EllipseGeometry();
                    to.Clip = irisClip;
                    break;
                case TransitionAnimationOptions.QuietCover:
                    to.RenderTransform = toTranslation;
                    break;
            }

            void Apply(double rawProgress)
            {
                var progress = Math.Clamp(rawProgress, 0, 1);
                var eased = easing.Ease(progress);

                switch (Name)
                {
                    case TransitionAnimationOptions.CleanPush:
                        to.ZIndex = 2;
                        fromTranslation.X = -direction * size.Width * eased;
                        toTranslation.X = direction * size.Width * (1 - eased);
                        break;

                    case TransitionAnimationOptions.EdgeWipe:
                    {
                        to.ZIndex = 2;
                        var width = size.Width * eased;
                        wipeClip!.Rect = forward
                            ? new Rect(0, 0, width, size.Height)
                            : new Rect(size.Width - width, 0, width, size.Height);
                        break;
                    }

                    case TransitionAnimationOptions.SoftIris:
                    {
                        to.ZIndex = 2;
                        var focusX = size.Width * (forward ? .64 : .36);
                        var focusY = size.Height * .46;
                        var radius = FarthestCornerRadius(
                            focusX,
                            focusY,
                            size.Width,
                            size.Height) * eased + .5;
                        irisClip!.Rect = new Rect(
                            focusX - radius,
                            focusY - radius,
                            radius * 2,
                            radius * 2);
                        break;
                    }

                    case TransitionAnimationOptions.QuietCover:
                        to.ZIndex = 2;
                        to.Opacity = .55 + (.45 * eased);
                        toTranslation.X = direction * size.Width * .05 * (1 - eased);
                        break;

                    case TransitionAnimationOptions.ExposureDip:
                        if (transitionShade is not null && shadeBrush is not null)
                        {
                            var shadeProgress = progress < .5
                                ? easing.Ease(progress * 2)
                                : 1 - easing.Ease((progress - .5) * 2);
                            var shadeAlpha = (byte)Math.Round(245 * shadeProgress);

                            from.ZIndex = progress < .5 ? 2 : 1;
                            to.ZIndex = progress < .5 ? 1 : 2;
                            from.Opacity = 1;
                            to.Opacity = 1;
                            shadeBrush.Color = Color.FromArgb(shadeAlpha, 0, 0, 0);
                            transitionShade.Background = shadeBrush;
                            transitionShade.InvalidateVisual();
                        }
                        else
                        {
                            to.ZIndex = 2;
                            if (progress < .5)
                            {
                                var outgoingProgress = easing.Ease(progress * 2);
                                from.Opacity = 1 - outgoingProgress;
                                to.Opacity = 0;
                            }
                            else
                            {
                                from.Opacity = 0;
                                to.Opacity = easing.Ease((progress - .5) * 2);
                            }
                        }
                        break;

                    default:
                        to.ZIndex = 2;
                        from.Opacity = 1 - eased;
                        to.Opacity = eased;
                        break;
                }

                Invalidate(from, to);
            }

            Apply(0);
            await RunFramesAsync(to, Apply, cancellationToken);
            Apply(1);
            completed = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // TransitioningContentControl owns cancellation and will select
            // which presenter participates in the replacement transition.
        }
        finally
        {
            fromState.Restore(from);
            toState.Restore(to);

            to.IsVisible = true;
            to.Opacity = 1;
            if (completed)
                from.IsVisible = false;
            if (transitionShade is not null)
            {
                transitionShade.Background = originalShadeBackground;
                transitionShade.InvalidateVisual();
            }

            Invalidate(from, to);
        }
    }

    private async Task RunFramesAsync(
        Visual anchor,
        Action<double> apply,
        CancellationToken cancellationToken)
    {
        if (Duration <= TimeSpan.Zero)
            return;

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < Duration)
        {
            await WaitForNextFrameAsync(anchor, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            apply(stopwatch.Elapsed.TotalMilliseconds / Duration.TotalMilliseconds);
        }
    }

    private static Task WaitForNextFrameAsync(
        Visual visual,
        CancellationToken cancellationToken)
    {
        var topLevel = TopLevel.GetTopLevel(visual);
        if (topLevel is null || cancellationToken.IsCancellationRequested)
            return Task.CompletedTask;

        var completion = new TaskCompletionSource<bool>();
        var registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));

        topLevel.RequestAnimationFrame(_ =>
        {
            registration.Dispose();
            completion.TrySetResult(true);
        });

        return completion.Task;
    }

    private static void Invalidate(Visual from, Visual to)
    {
        from.InvalidateVisual();
        to.InvalidateVisual();
    }

    private static Size TransitionSize(Visual visual)
    {
        return new Size(
            Math.Max(1, visual.Bounds.Width),
            Math.Max(1, visual.Bounds.Height));
    }

    private static double FarthestCornerRadius(
        double x,
        double y,
        double width,
        double height)
    {
        var distances = new[]
        {
            Math.Sqrt((x * x) + (y * y)),
            Math.Sqrt(((width - x) * (width - x)) + (y * y)),
            Math.Sqrt((x * x) + ((height - y) * (height - y))),
            Math.Sqrt(((width - x) * (width - x)) + ((height - y) * (height - y))),
        };
        return distances.Max();
    }

    private sealed record VisualState(
        double Opacity,
        bool IsVisible,
        int ZIndex,
        ITransform? RenderTransform,
        RelativePoint RenderTransformOrigin,
        Geometry? Clip)
    {
        public static VisualState Capture(Visual visual)
        {
            return new VisualState(
                visual.Opacity,
                visual.IsVisible,
                visual.ZIndex,
                visual.RenderTransform,
                visual.RenderTransformOrigin,
                visual.Clip);
        }

        public void Restore(Visual visual)
        {
            visual.Opacity = Opacity;
            visual.IsVisible = IsVisible;
            visual.ZIndex = ZIndex;
            visual.RenderTransform = RenderTransform;
            visual.RenderTransformOrigin = RenderTransformOrigin;
            visual.Clip = Clip;
        }
    }
}
