using K7.Server.Domain.Enums;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public static class BackgroundTaskStatusHelper
{
    public static Color GetColor(BackgroundTaskStatus status) => status switch
    {
        BackgroundTaskStatus.Pending => Color.Info,
        BackgroundTaskStatus.InProgress => Color.Warning,
        BackgroundTaskStatus.WaitingForRetry => Color.Secondary,
        BackgroundTaskStatus.Completed => Color.Success,
        BackgroundTaskStatus.Failed => Color.Error,
        _ => Color.Default
    };
}
