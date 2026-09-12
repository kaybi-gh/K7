namespace K7.Server.Domain.Models;

public sealed class NotificationScheduleWindow
{
    public List<DayOfWeek> Days { get; set; } = [];
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}
