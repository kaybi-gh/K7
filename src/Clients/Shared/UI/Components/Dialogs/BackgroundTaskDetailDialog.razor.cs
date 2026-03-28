using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class BackgroundTaskDetailDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter] public required BackgroundTaskDto Task { get; set; }

    private static Color GetStatusColor(BackgroundTaskStatus status) => status switch
    {
        BackgroundTaskStatus.Pending => Color.Info,
        BackgroundTaskStatus.InProgress => Color.Warning,
        BackgroundTaskStatus.WaitingForRetry => Color.Secondary,
        BackgroundTaskStatus.Completed => Color.Success,
        BackgroundTaskStatus.Failed => Color.Error,
        _ => Color.Default
    };

    private void Close() => MudDialog.Cancel();
}
