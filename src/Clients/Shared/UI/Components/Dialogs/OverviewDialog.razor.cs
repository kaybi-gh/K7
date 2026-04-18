using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class OverviewDialog
{
    [CascadingParameter] IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public string ContentText { get; set; } = string.Empty;

    [Parameter]
    public string ButtonText { get; set; } = "Close";

    private void Cancel()
    {
        Dialog.Cancel();
    }
}
