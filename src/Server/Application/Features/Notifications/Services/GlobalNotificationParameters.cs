namespace K7.Server.Application.Features.Notifications.Services;

public static class GlobalNotificationParameters
{
    public static readonly IReadOnlyList<NotificationParameterInfo> All =
    [
        new("EventType", "Event Type", "String"),
        new("Server.Name", "Server Name", "String"),
        new("Server.Url", "Server URL", "String"),
        new("Server.Version", "Server Version", "String"),
        new("Current.Year", "Current Year", "Int32"),
        new("Current.Month", "Current Month", "Int32"),
        new("Current.Day", "Current Day", "Int32"),
        new("Current.Hour", "Current Hour", "Int32"),
        new("Current.Minute", "Current Minute", "Int32"),
        new("Current.Weekday", "Current Weekday", "String"),
        new("Current.Datestamp", "Current Date", "String"),
        new("Current.Timestamp", "Current Timestamp", "String"),
    ];
}
