namespace K7.Shared.Dtos.Notifications;

public sealed record NotificationScheduleWindowDto
{
    public IReadOnlyList<int> Days { get; init; } = [];
    public string Start { get; init; } = "08:00";
    public string End { get; init; } = "22:00";
}
