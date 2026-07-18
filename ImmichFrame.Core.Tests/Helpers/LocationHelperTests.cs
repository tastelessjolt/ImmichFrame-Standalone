using ImmichFrame.Helpers;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Helpers;

[TestFixture]
public class LocationHelperTests
{
    [TestCase("IND", "🇮🇳")]
    [TestCase("IDN", "🇮🇩")]
    [TestCase("KOR", "🇰🇷")]
    [TestCase("USA", "🇺🇸")]
    public void GetCountryFlag_converts_iso3_codes_to_regional_indicator_flags(string countryCode, string expectedFlag)
    {
        Assert.That(LocationHelper.GetCountryFlag(countryCode), Is.EqualTo(expectedFlag));
    }

    [Test]
    public void GetCountryFlag_returns_empty_for_unknown_codes()
    {
        Assert.That(LocationHelper.GetCountryFlag("UNKNOWN"), Is.Empty);
    }

    [Test]
    public void GetCountryCode_normalizes_immichs_us_country_name()
    {
        Assert.That(LocationHelper.GetCountryCode("United States of America"), Is.EqualTo("USA"));
    }
}
