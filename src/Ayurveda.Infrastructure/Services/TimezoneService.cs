using System;
using Ayurveda.Application.Common;
using Ayurveda.Application.Interfaces;

namespace Ayurveda.Infrastructure.Services;

public class TimezoneService : ITimezoneService
{
    public DateTime ConvertUtcToIst(DateTime utc) => utc.ToIst();

    public DateTime? ConvertUtcToIst(DateTime? utc) => utc.ToIst();

    public DateTimeOffset ConvertUtcToIst(DateTimeOffset utcOffset) => utcOffset.ToIst();

    public DateTimeOffset? ConvertUtcToIst(DateTimeOffset? utcOffset) => utcOffset.ToIst();

    public string FormatIst(DateTime utc, string format = "dd MMM yyyy, hh:mm tt") => utc.ToIstString(format);

    public string FormatIst(DateTime? utc, string format = "dd MMM yyyy, hh:mm tt", string emptyFallback = "—") => utc.ToIstString(format, emptyFallback);

    public TimeZoneInfo GetIstTimeZone() => IndianTimezone.IstZone;
}

