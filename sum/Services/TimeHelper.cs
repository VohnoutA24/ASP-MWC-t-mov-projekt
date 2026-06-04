using System;

namespace sum.Services
{
    public static class TimeHelper
    {
        public static DateTime ToCzechTime(this DateTime utcDateTime)
        {
            // Ensure the date is UTC if it's Unspecified, otherwise ConvertTimeFromUtc throws an error
            if (utcDateTime.Kind == DateTimeKind.Unspecified)
            {
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }
            else if (utcDateTime.Kind == DateTimeKind.Local)
            {
                utcDateTime = utcDateTime.ToUniversalTime();
            }

            TimeZoneInfo tz;
            try
            {
                // Try Windows ID first
                tz = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback to IANA ID for Linux / Render
                tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
            }
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, tz);
        }
    }
}
