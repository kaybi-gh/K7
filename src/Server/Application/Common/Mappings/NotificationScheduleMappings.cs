using K7.Server.Domain.Models;
using K7.Shared.Dtos.Notifications;

namespace K7.Server.Application.Common.Mappings;

public static class NotificationScheduleMappings
{
    public static List<NotificationScheduleWindow> ToDomain(
        this IReadOnlyList<NotificationScheduleWindowDto>? windows)
    {
        if (windows is null || windows.Count == 0)
            return [];

        return windows.Select(w => new NotificationScheduleWindow
        {
            Days = w.Days.Select(d => (DayOfWeek)d).ToList(),
            Start = TimeOnly.TryParse(w.Start, out var start) ? start : TimeOnly.MinValue,
            End = TimeOnly.TryParse(w.End, out var end) ? end : new TimeOnly(23, 59)
        }).ToList();
    }

    public static IReadOnlyList<NotificationScheduleWindowDto> ToDto(
        this IReadOnlyList<NotificationScheduleWindow>? windows)
    {
        if (windows is null || windows.Count == 0)
            return [];

        return windows.Select(w => new NotificationScheduleWindowDto
        {
            Days = w.Days.Select(d => (int)d).ToList(),
            Start = w.Start.ToString("HH:mm"),
            End = w.End.ToString("HH:mm")
        }).ToList();
    }
}
