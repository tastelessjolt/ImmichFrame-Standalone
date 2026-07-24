using ImmichFrame.ViewModels;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Settings;

[TestFixture]
public class PersonListItemTests
{
    [Test]
    public void UnnamedPeopleRemainVisibleWithAUsefulPlaceholder()
    {
        var item = new PersonListItem(Guid.NewGuid(), string.Empty, true);

        Assert.Multiple(() =>
        {
            Assert.That(item.DisplayName, Is.EqualTo("Unnamed person"));
            Assert.That(item.Detail, Is.EqualTo("No name in Immich"));
            Assert.That(item.Initials, Is.EqualTo("?"));
            Assert.That(item.HasDetail, Is.True);
            Assert.That(item.IsUnnamedGridCandidate, Is.True);
            Assert.That(item.IsThumbnailPlaceholderVisible, Is.True);
        });
    }

    [Test]
    public void NamedPeopleUseCompactInitialsUntilTheirFaceLoads()
    {
        var item = new PersonListItem(Guid.NewGuid(), "Alex Example", true);

        Assert.Multiple(() =>
        {
            Assert.That(item.DisplayName, Is.EqualTo("Alex Example"));
            Assert.That(item.Detail, Is.Empty);
            Assert.That(item.Initials, Is.EqualTo("AE"));
            Assert.That(item.HasDetail, Is.False);
            Assert.That(item.IsUnnamedGridCandidate, Is.False);
        });
    }

    [Test]
    public void SavedUnresolvedPeopleHaveAnExplicitFallback()
    {
        var item = new PersonListItem(Guid.NewGuid(), null, false);

        Assert.Multiple(() =>
        {
            Assert.That(item.Detail, Is.EqualTo("Saved person · name unavailable"));
            Assert.That(item.IsUnnamedGridCandidate, Is.False);
        });
    }

    [Test]
    public void SelectionStateUsesShortLabelsSuitableForFaceGridCells()
    {
        var item = new PersonListItem(Guid.NewGuid(), string.Empty, true);

        item.SelectionState = PersonSelectionState.Excluded;

        Assert.That(item.SelectionLabel, Is.EqualTo("Excluded"));
    }

    [Test]
    public void FaceGridRowsPreserveServerOrderAndUseTheRequestedColumnCount()
    {
        var people = Enumerable.Range(0, 23)
            .Select(index => new PersonListItem(
                Guid.NewGuid(),
                string.Empty,
                true,
                sortIndex: index))
            .ToList();

        var rows = PersonGridRow.Create(people, 10);

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(3));
            Assert.That(rows.Select(row => row.People.Count), Is.EqualTo(new[] { 10, 10, 3 }));
            Assert.That(rows.SelectMany(row => row.People), Is.EqualTo(people));
        });
    }
}
