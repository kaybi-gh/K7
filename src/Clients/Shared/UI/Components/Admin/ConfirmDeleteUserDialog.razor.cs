using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class ConfirmDeleteUserDialog
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public string DisplayName { get; set; } = "";

    private void Cancel() => MudDialog.Cancel();
    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
}
