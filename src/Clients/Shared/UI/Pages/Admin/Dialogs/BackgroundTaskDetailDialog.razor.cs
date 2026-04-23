using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class BackgroundTaskDetailDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public required BackgroundTaskDto BackgroundTask { get; set; }

    private void Close() => Dialog.Cancel();
}
