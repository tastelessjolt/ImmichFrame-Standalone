using ImmichFrame.Helpers;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Helpers;

[TestFixture]
public class UiResourceLifetimeTests
{
    [TestCase(2, 3)]
    [TestCase(0, 1)]
    [TestCase(-5, 1)]
    public void AfterTransitionKeepsResourcesForTransitionAndSafetyMargin(
        double transitionSeconds,
        double expectedSeconds)
    {
        Assert.That(
            UiResourceLifetime.AfterTransition(transitionSeconds),
            Is.EqualTo(TimeSpan.FromSeconds(expectedSeconds)));
    }
}
