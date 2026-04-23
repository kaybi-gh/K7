using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class ResetPasswordDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public required UserDto User { get; set; }

    private string _newPassword = "";
    private bool _isSubmitting;

    private void Cancel() => Dialog.Cancel();

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
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
