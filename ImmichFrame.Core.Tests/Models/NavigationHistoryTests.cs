using ImmichFrame.Models;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Models;

[TestFixture]
public class NavigationHistoryTests
{
    [Test]
    public void PreviousCanWalkBackMultipleEntries()
    {
        var history = new NavigationHistory<string>(10);
        history.Add("one");
        history.Add("two");
        history.Add("three");

        Assert.Multiple(() =>
        {
            Assert.That(history.TryMovePrevious(out var second), Is.True);
            Assert.That(second, Is.EqualTo("two"));
            Assert.That(history.TryMovePrevious(out var first), Is.True);
            Assert.That(first, Is.EqualTo("one"));
            Assert.That(history.TryMovePrevious(out _), Is.False);
            Assert.That(history.Current, Is.EqualTo("one"));
        });
    }

    [Test]
    public void NextMovesForwardAfterPrevious()
    {
        var history = new NavigationHistory<string>(10);
        history.Add("one");
        history.Add("two");
        history.Add("three");
        history.TryMovePrevious(out _);
        history.TryMovePrevious(out _);

        Assert.Multiple(() =>
        {
            Assert.That(history.TryMoveNext(out var second), Is.True);
            Assert.That(second, Is.EqualTo("two"));
            Assert.That(history.TryMoveNext(out var third), Is.True);
            Assert.That(third, Is.EqualTo("three"));
            Assert.That(history.TryMoveNext(out _), Is.False);
        });
    }

    [Test]
    public void AddingAfterPreviousDiscardsForwardBranch()
    {
        var history = new NavigationHistory<string>(10);
        history.Add("one");
        history.Add("two");
        history.Add("three");
        history.TryMovePrevious(out _);

        history.Add("replacement");

        Assert.Multiple(() =>
        {
            Assert.That(history.Current, Is.EqualTo("replacement"));
            Assert.That(history.TryMoveNext(out _), Is.False);
            Assert.That(history.TryMovePrevious(out var previous), Is.True);
            Assert.That(previous, Is.EqualTo("two"));
        });
    }

    [Test]
    public void CapacityDiscardsOldestEntries()
    {
        var history = new NavigationHistory<string>(3);
        history.Add("one");
        history.Add("two");
        history.Add("three");
        history.Add("four");

        Assert.Multiple(() =>
        {
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.TryMovePrevious(out var third), Is.True);
            Assert.That(third, Is.EqualTo("three"));
            Assert.That(history.TryMovePrevious(out var second), Is.True);
            Assert.That(second, Is.EqualTo("two"));
            Assert.That(history.TryMovePrevious(out _), Is.False);
        });
    }

    [Test]
    public void CapacityMustBePositive()
    {
        Assert.That(() => new NavigationHistory<string>(0),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
