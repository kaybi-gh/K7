using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class OverviewDialog
{
    [CascadingParameter]
    IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public string ContentText { get; set; } = string.Empty;

    [Parameter]
    public string ButtonText { get; set; } = "Close";

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
