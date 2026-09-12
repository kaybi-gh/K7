using K7.Server.Domain.Models;

namespace K7.Server.Application.Features.Notifications.Services;

public static class NotificationScheduleEvaluator
{
    public static bool IsWithinWindow(
        IReadOnlyList<NotificationScheduleWindow>? windows,
        DateTimeOffset now)
    {
        if (windows is null || windows.Count == 0)
            return true;

        var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local);
        var time = TimeOnly.FromDateTime(local.DateTime);
        var day = local.DayOfWeek;

        return windows.Any(window => Matches(window, day, time));
    }

    private static bool Matches(NotificationScheduleWindow window, DayOfWeek day, TimeOnly time)
    {
        if (window.Days.Count > 0 && !window.Days.Contains(day))
            return false;

        if (window.Start <= window.End)
            return time >= window.Start && time <= window.End;

        return time >= window.Start || time <= window.End;
    }
}
