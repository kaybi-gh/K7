using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public static class BackgroundTaskStatusHelper
{
    public static string GetColor(BackgroundTaskStatus status) => status switch
    {
        BackgroundTaskStatus.Pending => "info",
        BackgroundTaskStatus.InProgress => "warning",
        BackgroundTaskStatus.WaitingForRetry => "secondary",
        BackgroundTaskStatus.Completed => "success",
        BackgroundTaskStatus.Failed => "error",
        BackgroundTaskStatus.Cancelled => "warning",
        _ => "default"
    };
}
