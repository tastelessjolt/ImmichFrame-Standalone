using ImmichFrame.Animations;
using ImmichFrame.Models;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Helpers;

[TestFixture]
public class PhotoTransitionFactoryTests
{
    [Test]
    public void EveryConfiguredOptionCreatesATransition()
    {
        Assert.That(TransitionAnimationOptions.All, Has.Count.EqualTo(6));
        Assert.That(TransitionAnimationOptions.All, Is.Unique);

        foreach (var option in TransitionAnimationOptions.All)
        {
            var transition = PhotoTransitionFactory.Create(
                option,
                TimeSpan.FromMilliseconds(520));

            Assert.Multiple(() =>
            {
                Assert.That(transition, Is.TypeOf<FrameDrivenPhotoTransition>(), option);
                Assert.That(((FrameDrivenPhotoTransition)transition).Name, Is.EqualTo(option));
                Assert.That(
                    ((FrameDrivenPhotoTransition)transition).Duration,
                    Is.EqualTo(TimeSpan.FromMilliseconds(520)));
            });
        }
    }

    [Test]
    public void LegacyAndUnknownSettingsFallBackToCrossfade()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ((FrameDrivenPhotoTransition)PhotoTransitionFactory.Create(
                    TransitionAnimationOptions.Crossfade,
                    TimeSpan.Zero)).Name,
                Is.EqualTo(TransitionAnimationOptions.Crossfade));
            Assert.That(
                ((FrameDrivenPhotoTransition)PhotoTransitionFactory.Create(
                    "Future transition",
                    TimeSpan.FromMilliseconds(-1))).Name,
                Is.EqualTo(TransitionAnimationOptions.Crossfade));
            Assert.That(
                ((FrameDrivenPhotoTransition)PhotoTransitionFactory.Create(
                    "Future transition",
                    TimeSpan.FromMilliseconds(-1))).Duration,
                Is.EqualTo(TimeSpan.Zero));
        });
    }

    [Test]
    public void CleanPushUsesTheFrameDrivenRenderer()
    {
        var transition = (FrameDrivenPhotoTransition)PhotoTransitionFactory.Create(
            TransitionAnimationOptions.CleanPush,
            TimeSpan.FromMilliseconds(520));

        Assert.That(transition.Name, Is.EqualTo(TransitionAnimationOptions.CleanPush));
    }
}
