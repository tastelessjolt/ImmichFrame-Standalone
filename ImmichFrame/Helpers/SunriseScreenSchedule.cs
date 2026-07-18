using System;

namespace ImmichFrame.Helpers;

public sealed record SolarTimes(DateTimeOffset? Sunrise, DateTimeOffset? Sunset);

public sealed record SunriseScreenSchedulePlan(
    DateTimeOffset NextSleep,
    DateTimeOffset NextWake,
    bool ShouldBeAsleep,
    SolarTimes Today);

/// <summary>
/// Calculates local sunrise and sunset without a network request. The formula
/// is the compact NOAA sunrise equation and is accurate to within a few minutes,
/// which is more than sufficient for display power scheduling.
/// </summary>
public static class SunriseScreenSchedule
{
    private const double OfficialZenithDegrees = 90.833;

    public static SunriseScreenSchedulePlan? CreatePlan(
        DateTimeOffset now,
        double latitude,
        double longitude,
        TimeZoneInfo timeZone)
    {
        ValidateCoordinates(latitude, longitude);

        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var localDate = localNow.Date;
        var today = CalculateSolarTimes(localDate, latitude, longitude, timeZone);
        var nextWake = FindNextSunrise(localNow, latitude, longitude, timeZone);
        if (nextWake == null)
            return null;

        var nextSleep = AtLocalTime(localDate.AddDays(1), TimeSpan.Zero, timeZone);
        var shouldBeAsleep = today.Sunrise is { } sunrise && localNow < sunrise;

        return new SunriseScreenSchedulePlan(nextSleep, nextWake.Value, shouldBeAsleep, today);
    }

    public static SolarTimes CalculateSolarTimes(
        DateTime localDate,
        double latitude,
        double longitude,
        TimeZoneInfo timeZone)
    {
        ValidateCoordinates(latitude, longitude);
        localDate = localDate.Date;

        return new SolarTimes(
            CalculateSolarEvent(localDate, latitude, longitude, timeZone, sunrise: true),
            CalculateSolarEvent(localDate, latitude, longitude, timeZone, sunrise: false));
    }

    private static DateTimeOffset? FindNextSunrise(
        DateTimeOffset localNow,
        double latitude,
        double longitude,
        TimeZoneInfo timeZone)
    {
        // Searching forward also handles the polar-day/polar-night edge case.
        for (var dayOffset = 0; dayOffset <= 370; dayOffset++)
        {
            var date = localNow.Date.AddDays(dayOffset);
            var sunrise = CalculateSolarTimes(date, latitude, longitude, timeZone).Sunrise;
            if (sunrise != null && sunrise > localNow.AddMinutes(1))
                return sunrise;
        }

        return null;
    }

    private static DateTimeOffset? CalculateSolarEvent(
        DateTime localDate,
        double latitude,
        double longitude,
        TimeZoneInfo timeZone,
        bool sunrise)
    {
        var dayOfYear = localDate.DayOfYear;
        var longitudeHour = longitude / 15d;
        var approximateTime = dayOfYear + ((sunrise ? 6d : 18d) - longitudeHour) / 24d;
        var meanAnomaly = 0.9856d * approximateTime - 3.289d;
        var trueLongitude = NormalizeDegrees(
            meanAnomaly
            + 1.916d * SinDegrees(meanAnomaly)
            + 0.020d * SinDegrees(2d * meanAnomaly)
            + 282.634d);

        var rightAscension = NormalizeDegrees(ToDegrees(Math.Atan(0.91764d * Math.Tan(ToRadians(trueLongitude)))));
        var longitudeQuadrant = Math.Floor(trueLongitude / 90d) * 90d;
        var rightAscensionQuadrant = Math.Floor(rightAscension / 90d) * 90d;
        rightAscension = (rightAscension + longitudeQuadrant - rightAscensionQuadrant) / 15d;

        var sinDeclination = 0.39782d * SinDegrees(trueLongitude);
        var cosDeclination = Math.Cos(Math.Asin(sinDeclination));
        var cosHourAngle =
            (CosDegrees(OfficialZenithDegrees) - sinDeclination * SinDegrees(latitude))
            / (cosDeclination * CosDegrees(latitude));

        if (cosHourAngle is > 1d or < -1d)
            return null;

        var hourAngle = sunrise
            ? 360d - ToDegrees(Math.Acos(cosHourAngle))
            : ToDegrees(Math.Acos(cosHourAngle));
        hourAngle /= 15d;

        var localMeanTime = hourAngle + rightAscension - 0.06571d * approximateTime - 6.622d;
        var utcHour = NormalizeHours(localMeanTime - longitudeHour);
        var utc = new DateTimeOffset(DateTime.SpecifyKind(localDate, DateTimeKind.Utc)).AddHours(utcHour);
        var local = TimeZoneInfo.ConvertTime(utc, timeZone);

        // For locations east of UTC, sunrise can belong to the previous UTC
        // date. Anchor the result to the requested local calendar date.
        while (local.Date < localDate)
        {
            utc = utc.AddDays(1);
            local = TimeZoneInfo.ConvertTime(utc, timeZone);
        }

        while (local.Date > localDate)
        {
            utc = utc.AddDays(-1);
            local = TimeZoneInfo.ConvertTime(utc, timeZone);
        }

        return local;
    }

    private static DateTimeOffset AtLocalTime(DateTime date, TimeSpan time, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
    }

    private static void ValidateCoordinates(double latitude, double longitude)
    {
        if (latitude is < -90d or > 90d)
            throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180d or > 180d)
            throw new ArgumentOutOfRangeException(nameof(longitude));
    }

    private static double NormalizeDegrees(double value) => ((value % 360d) + 360d) % 360d;
    private static double NormalizeHours(double value) => ((value % 24d) + 24d) % 24d;
    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
    private static double ToDegrees(double radians) => radians * 180d / Math.PI;
    private static double SinDegrees(double degrees) => Math.Sin(ToRadians(degrees));
    private static double CosDegrees(double degrees) => Math.Cos(ToRadians(degrees));
}
