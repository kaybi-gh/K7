using K7.Clients.Shared.UI.Components.Admin;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class BackgroundTaskDetailDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter] public required BackgroundTaskDto Task { get; set; }

    private void Close() => MudDialog.Cancel();
}
