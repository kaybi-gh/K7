using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class ConfirmDeleteNotificationRuleDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;
    [Parameter] public string DisplayName { get; set; } = "";

    private void Cancel() => Dialog.Cancel();
    private void Confirm() => Dialog.Close(K7DialogResult.Ok(true));
}
