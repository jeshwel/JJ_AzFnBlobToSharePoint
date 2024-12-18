using System;

namespace JJ_AzFnBlobToSharePoint.Core.Extensions
{
    public static class DateExtensions
    {
        //Azure SDK requires it to be UTC. You can call DateTime.SpecifyKind to change Kind property value to DateTimeKind.Utc.
        /// <summary>
        ///Minimum value for DateTime type attribute supported by Azure Tables is Jan 1, 1600 UTC.
        ///Thus setting the .net default value DateTime.MinValue will result in an error.
        /// </summary>
        /// <returns>1900-01-01T00:00:00Z</returns>
        public static DateTime CustomDateMinValue { get { return new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc); } }
        public static DateTime ConvertUtcToEST(this DateTime utcTime)
        {
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, easternZone);
        }
        public static bool IsDateMinValue(this DateTime utcTime)
        {
            return utcTime == CustomDateMinValue;
        }
    }
}
