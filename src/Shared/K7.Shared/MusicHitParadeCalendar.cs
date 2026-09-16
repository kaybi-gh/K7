using K7.Shared.Dtos;

namespace K7.Shared;

public readonly record struct MusicHitParadeWindow(DateTimeOffset From, DateTimeOffset To);

public static class MusicHitParadeCalendar
{
    private static readonly string[] Seasons =
    [
        MusicHitParadeSeasons.Winter,
        MusicHitParadeSeasons.Spring,
        MusicHitParadeSeasons.Summer,
        MusicHitParadeSeasons.Autumn
    ];

    public static MusicHitParadeWindow? GetWindow(
        string period,
        int year,
        int month = 1,
        string? season = null,
        TimeZoneInfo? timeZone = null)
    {
        var tz = timeZone ?? TimeZoneInfo.Utc;
        return NormalizePeriod(period) switch
        {
            MusicHitParadePeriods.Year => CreateWindow(year, 1, 1, year + 1, 1, 1, tz),
            MusicHitParadePeriods.Month => CreateMonthWindow(year, month, tz),
            MusicHitParadePeriods.Season => CreateSeasonWindow(year, season, tz),
            _ => null
        };
    }

    public static MusicHitParadeWindow GetCustomWindow(DateOnly from, DateOnly to, TimeZoneInfo? timeZone = null)
    {
        var tz = timeZone ?? TimeZoneInfo.Utc;
        if (to < from)
            (from, to) = (to, from);

        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return CreateWindow(start.Year, start.Month, start.Day, end.Year, end.Month, end.Day, tz);
    }

    public static string NormalizePeriod(string? period) =>
        period?.Trim().ToLowerInvariant() switch
        {
            MusicHitParadePeriods.Year => MusicHitParadePeriods.Year,
            MusicHitParadePeriods.Season => MusicHitParadePeriods.Season,
            MusicHitParadePeriods.Month => MusicHitParadePeriods.Month,
            MusicHitParadePeriods.Custom => MusicHitParadePeriods.Custom,
            _ => MusicHitParadePeriods.All
        };

    public static string NormalizeScope(string? scope) =>
        string.Equals(scope, MusicHitParadeScopes.Server, StringComparison.OrdinalIgnoreCase)
            ? MusicHitParadeScopes.Server
            : MusicHitParadeScopes.Personal;

    public static string NormalizeSeason(string? season) =>
        season?.Trim().ToLowerInvariant() switch
        {
            MusicHitParadeSeasons.Spring => MusicHitParadeSeasons.Spring,
            MusicHitParadeSeasons.Summer => MusicHitParadeSeasons.Summer,
            MusicHitParadeSeasons.Autumn or "fall" => MusicHitParadeSeasons.Autumn,
            _ => MusicHitParadeSeasons.Winter
        };

    public static (int Year, string Season) SeasonFor(DateTime localDate) =>
        localDate.Month switch
        {
            12 => (localDate.Year + 1, MusicHitParadeSeasons.Winter),
            1 or 2 => (localDate.Year, MusicHitParadeSeasons.Winter),
            3 or 4 or 5 => (localDate.Year, MusicHitParadeSeasons.Spring),
            6 or 7 or 8 => (localDate.Year, MusicHitParadeSeasons.Summer),
            _ => (localDate.Year, MusicHitParadeSeasons.Autumn)
        };

    public static (int Year, int Month) ShiftMonth(int year, int month, int delta)
    {
        var clampedMonth = Math.Clamp(month, 1, 12);
        var next = new DateTime(year, clampedMonth, 1).AddMonths(delta);
        return (next.Year, next.Month);
    }

    public static (int Year, string Season) ShiftSeason(int year, string season, int delta)
    {
        var normalized = NormalizeSeason(season);
        var index = Array.IndexOf(Seasons, normalized);
        if (index < 0)
            index = 0;

        var next = index + delta;
        var yearShift = (int)Math.Floor(next / (double)Seasons.Length);
        var wrapped = ((next % Seasons.Length) + Seasons.Length) % Seasons.Length;
        return (year + yearShift, Seasons[wrapped]);
    }

    public static bool IsFuturePeriod(
        string period,
        int year,
        int month,
        string? season,
        DateTimeOffset now,
        TimeZoneInfo? timeZone = null)
    {
        var window = GetWindow(period, year, month, season, timeZone);
        return window is not null && window.Value.From > now;
    }

    public static bool HasNextPeriod(
        string period,
        int year,
        int month,
        string? season,
        DateTimeOffset now,
        TimeZoneInfo? timeZone = null)
    {
        var nextYear = year;
        var nextMonth = month;
        var nextSeason = season;

        switch (NormalizePeriod(period))
        {
            case MusicHitParadePeriods.Year:
                nextYear++;
                break;
            case MusicHitParadePeriods.Month:
                (nextYear, nextMonth) = ShiftMonth(year, month, 1);
                break;
            case MusicHitParadePeriods.Season:
                (nextYear, nextSeason) = ShiftSeason(year, season ?? MusicHitParadeSeasons.Winter, 1);
                break;
            default:
                return false;
        }

        var window = GetWindow(period, nextYear, nextMonth, nextSeason, timeZone);
        return window is not null && window.Value.From <= now;
    }

    private static MusicHitParadeWindow CreateMonthWindow(int year, int month, TimeZoneInfo timeZone)
    {
        var clampedMonth = Math.Clamp(month, 1, 12);
        var start = new DateTime(year, clampedMonth, 1);
        var end = start.AddMonths(1);
        return CreateWindow(start.Year, start.Month, 1, end.Year, end.Month, 1, timeZone);
    }

    private static MusicHitParadeWindow CreateSeasonWindow(int year, string? season, TimeZoneInfo timeZone) =>
        NormalizeSeason(season) switch
        {
            MusicHitParadeSeasons.Spring => CreateWindow(year, 3, 1, year, 6, 1, timeZone),
            MusicHitParadeSeasons.Summer => CreateWindow(year, 6, 1, year, 9, 1, timeZone),
            MusicHitParadeSeasons.Autumn => CreateWindow(year, 9, 1, year, 12, 1, timeZone),
            _ => CreateWindow(year - 1, 12, 1, year, 3, 1, timeZone)
        };

    private static MusicHitParadeWindow CreateWindow(
        int fromYear,
        int fromMonth,
        int fromDay,
        int toYear,
        int toMonth,
        int toDay,
        TimeZoneInfo timeZone)
    {
        var fromLocal = new DateTime(fromYear, fromMonth, fromDay, 0, 0, 0, DateTimeKind.Unspecified);
        var toLocal = new DateTime(toYear, toMonth, toDay, 0, 0, 0, DateTimeKind.Unspecified);
        var from = new DateTimeOffset(fromLocal, timeZone.GetUtcOffset(fromLocal));
        var to = new DateTimeOffset(toLocal, timeZone.GetUtcOffset(toLocal));
        return new MusicHitParadeWindow(from, to);
    }
}
