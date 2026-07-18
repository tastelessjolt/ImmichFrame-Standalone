using System.Reflection;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Settings;

[TestFixture]
public class SettingsCompatibilityTests
{
    [Test]
    public void ParseSettings_parses_people_exclusions_and_ignores_unknown_properties()
    {
        var excludedPerson = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var values = new Dictionary<string, object>
        {
            ["ImmichServerUrl"] = "https://immich.example/",
            ["ApiKey"] = "test-key",
            ["ClockFontWeight"] = "Normal",
            ["LetterboxBackground"] = "Stretched thumbhash",
            ["ExcludedPeople"] = new List<string> { excludedPerson.ToString() },
            ["Webcalendars"] = new List<string>(),
        };

        var parseSettings = typeof(ImmichFrame.Models.Settings).GetMethod(
            "ParseSettings",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(parseSettings, Is.Not.Null);

        var settings = (ImmichFrame.Models.Settings?)parseSettings!.Invoke(null, [values]);

        Assert.Multiple(() =>
        {
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings!.ImmichServerUrl, Is.EqualTo("https://immich.example"));
            Assert.That(settings.ApiKey, Is.EqualTo("test-key"));
            Assert.That(settings.LetterboxBackground, Is.EqualTo("Stretched thumbhash"));
            Assert.That(settings.ExcludedPeople, Is.EqualTo(new[] { excludedPerson }));
        });
    }
}
