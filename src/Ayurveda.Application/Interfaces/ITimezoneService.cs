namespace Ayurveda.Application.Interfaces;

public interface ITimezoneService
{
    DateTime ConvertUtcToIst(DateTime utc);
    DateTime? ConvertUtcToIst(DateTime? utc);
    DateTimeOffset ConvertUtcToIst(DateTimeOffset utcOffset);
    DateTimeOffset? ConvertUtcToIst(DateTimeOffset? utcOffset);
    string FormatIst(DateTime utc, string format = "dd MMM yyyy, hh:mm tt");
    string FormatIst(DateTime? utc, string format = "dd MMM yyyy, hh:mm tt", string emptyFallback = "—");
    TimeZoneInfo GetIstTimeZone();
}

