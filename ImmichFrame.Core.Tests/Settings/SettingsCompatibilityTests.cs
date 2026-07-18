using System.Reflection;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Settings;

[TestFixture]
public class SettingsCompatibilityTests
{
    [Test]
    public void ParseSettings_ignores_properties_from_newer_or_custom_builds()
    {
        var values = new Dictionary<string, object>
        {
            ["ImmichServerUrl"] = "https://immich.example/",
            ["ApiKey"] = "test-key",
            ["ClockFontWeight"] = "Normal",
            ["ExcludedPeople"] = new List<string>(),
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
        });
    }
}
