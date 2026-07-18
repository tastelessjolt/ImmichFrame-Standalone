using ImmichFrame.Helpers;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Helpers;

[TestFixture]
public class SunriseScreenScheduleTests
{
    private static readonly TimeZoneInfo KoreaTime = TimeZoneInfo.CreateCustomTimeZone(
        "KST",
        TimeSpan.FromHours(9),
        "Korea Standard Time",
        "Korea Standard Time");

    [Test]
    public void CalculateSolarTimes_uses_the_weather_location_and_local_date()
    {
        var solarTimes = SunriseScreenSchedule.CalculateSolarTimes(
            new DateTime(2026, 7, 19),
            37.4989,
            127.0202,
            KoreaTime);

        Assert.Multiple(() =>
        {
            Assert.That(solarTimes.Sunrise, Is.Not.Null);
            Assert.That(solarTimes.Sunset, Is.Not.Null);
            Assert.That(solarTimes.Sunrise!.Value.Date, Is.EqualTo(new DateTime(2026, 7, 19)));
            Assert.That(solarTimes.Sunset!.Value.Date, Is.EqualTo(new DateTime(2026, 7, 19)));
            Assert.That(
                Math.Abs((solarTimes.Sunrise.Value.TimeOfDay - new TimeSpan(5, 25, 0)).TotalMinutes),
                Is.LessThan(10));
            Assert.That(
                Math.Abs((solarTimes.Sunset.Value.TimeOfDay - new TimeSpan(19, 49, 0)).TotalMinutes),
                Is.LessThan(10));
        });
    }

    [Test]
    public void CreatePlan_before_sunrise_sleeps_until_todays_sunrise()
    {
        var now = new DateTimeOffset(2026, 7, 19, 3, 0, 0, TimeSpan.FromHours(9));

        var plan = SunriseScreenSchedule.CreatePlan(now, 37.4989, 127.0202, KoreaTime);

        Assert.Multiple(() =>
        {
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan!.ShouldBeAsleep, Is.True);
            Assert.That(plan.NextWake.Date, Is.EqualTo(new DateTime(2026, 7, 19)));
            Assert.That(plan.NextSleep, Is.EqualTo(new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.FromHours(9))));
        });
    }

    [Test]
    public void CreatePlan_after_sunrise_keeps_screen_on_and_uses_tomorrows_sunrise()
    {
        var now = new DateTimeOffset(2026, 7, 19, 8, 0, 0, TimeSpan.FromHours(9));

        var plan = SunriseScreenSchedule.CreatePlan(now, 37.4989, 127.0202, KoreaTime);

        Assert.Multiple(() =>
        {
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan!.ShouldBeAsleep, Is.False);
            Assert.That(plan.NextWake.Date, Is.EqualTo(new DateTime(2026, 7, 20)));
            Assert.That(plan.NextSleep, Is.EqualTo(new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.FromHours(9))));
        });
    }

    [Test]
    public void CalculateSolarTimes_returns_no_events_during_polar_day()
    {
        var solarTimes = SunriseScreenSchedule.CalculateSolarTimes(
            new DateTime(2026, 6, 21),
            90,
            0,
            TimeZoneInfo.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(solarTimes.Sunrise, Is.Null);
            Assert.That(solarTimes.Sunset, Is.Null);
        });
    }
}
