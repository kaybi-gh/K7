using K7.Clients.Shared.UI.Components.Admin;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class BackgroundTaskDetailDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public required BackgroundTaskDto Task { get; set; }

    private void Close() => Dialog.Cancel();
}
