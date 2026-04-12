using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class ResetPasswordDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter] public required UserDto User { get; set; }

    private string _newPassword = "";
    private bool _isSubmitting;

    private void Cancel() => MudDialog.Cancel();

    private async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(_newPassword)) return;

        _isSubmitting = true;
        try
        {
            await K7ServerService.ResetUserPasswordAsync(User.Id, new ResetUserPasswordRequest
            {
                NewPassword = _newPassword
            });
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
