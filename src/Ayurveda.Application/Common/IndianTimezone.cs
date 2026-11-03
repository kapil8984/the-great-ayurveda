using System;

namespace Ayurveda.Application.Common;

public static class IndianTimezone
{
    public static readonly TimeZoneInfo IstZone;

    static IndianTimezone()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("India Standard Time", out var tz))
        {
            IstZone = tz;
        }
        else if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Kolkata", out tz))
        {
            IstZone = tz;
        }
        else
        {
            IstZone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "(UTC+05:30) India Standard Time", "India Standard Time");
        }
    }

    public static DateTime ToIst(this DateTime utcDateTime)
    {
        if (utcDateTime == DateTime.MinValue || utcDateTime == DateTime.MaxValue)
            return utcDateTime;

        var utc = utcDateTime.Kind switch
        {
            DateTimeKind.Utc => utcDateTime,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
            DateTimeKind.Local => utcDateTime.ToUniversalTime(),
            _ => utcDateTime
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, IstZone);
    }

    public static DateTime? ToIst(this DateTime? utcDateTime)
    {
        if (!utcDateTime.HasValue) return null;
        return utcDateTime.Value.ToIst();
    }

    public static DateTimeOffset ToIst(this DateTimeOffset utcDateTimeOffset)
    {
        return TimeZoneInfo.ConvertTime(utcDateTimeOffset, IstZone);
    }

    public static DateTimeOffset? ToIst(this DateTimeOffset? utcDateTimeOffset)
    {
        if (!utcDateTimeOffset.HasValue) return null;
        return utcDateTimeOffset.Value.ToIst();
    }

    public static string ToIstString(this DateTime utcDateTime, string format = "dd MMM yyyy, hh:mm tt")
    {
        return utcDateTime.ToIst().ToString(format) + " IST";
    }

    public static string ToIstString(this DateTime? utcDateTime, string format = "dd MMM yyyy, hh:mm tt", string emptyFallback = "—")
    {
        return utcDateTime.HasValue ? utcDateTime.Value.ToIstString(format) : emptyFallback;
    }
}

