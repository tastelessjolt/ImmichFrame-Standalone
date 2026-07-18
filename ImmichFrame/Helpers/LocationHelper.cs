using ImmichFrame.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ImmichFrame.Helpers
{
    public class LocationHelper
    {
        private static readonly List<RegionInfo> countriesInfo;
        private static readonly IReadOnlyDictionary<string, string> alpha2ByAlpha3;

        static LocationHelper()
        {
            countriesInfo = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .Select(culture => new RegionInfo(culture.Name))
                .GroupBy(region => region.ThreeLetterISORegionName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            alpha2ByAlpha3 = countriesInfo.ToDictionary(
                region => region.ThreeLetterISORegionName,
                region => region.TwoLetterISORegionName,
                StringComparer.OrdinalIgnoreCase);
        }

        public static string GetCountryCode(string country)
        {
            string target_country = country;
            if (country == "United States of America")
            {
                target_country = "United States";
            }
            RegionInfo? countryInfo = countriesInfo.FirstOrDefault(info =>
                string.Equals(info.EnglishName, target_country, StringComparison.OrdinalIgnoreCase));
            return countryInfo?.ThreeLetterISORegionName ?? country;
        }

        public static string GetCountryFlag(string threeLetterCountryCode)
        {
            if (!alpha2ByAlpha3.TryGetValue(threeLetterCountryCode, out var alpha2) || alpha2.Length != 2)
                return string.Empty;

            return string.Concat(alpha2.ToUpperInvariant().Select(character =>
                char.ConvertFromUtf32(0x1F1E6 + character - 'A')));
        }

        public static string GetLocationString(ImmichFrame.Core.Api.ExifResponseDto exifInfo)
        {
            var locationParts = Settings.CurrentSettings.ImageLocationFormat?.Split(',') ?? Array.Empty<string>();

            var city = locationParts.Length >= 1 ? exifInfo.City : string.Empty;
            var state = locationParts.Length >= 2 ? (exifInfo.State?.Split(", ").Last() ?? string.Empty) : string.Empty;
            var country = string.Empty;
            if (locationParts.Length >= 3 && !string.IsNullOrWhiteSpace(exifInfo.Country))
            {
                var countryCode = GetCountryCode(exifInfo.Country);
                var countryFlag = GetCountryFlag(countryCode);
                country = string.IsNullOrEmpty(countryFlag) ? countryCode : $"{countryCode} {countryFlag}";
            }

            return string.Join(", ", new[] { city, state, country }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}
