using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class CreateUserDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    private string _username = "";
    private string _password = "";
    private string _role = "User";
    private bool _isSubmitting;

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(_username)) return;

        _isSubmitting = true;
        try
        {
            var user = await K7ServerService.CreateUserAsync(new CreateUserRequest
            {
                Username = _username.Trim(),
                Role = _role,
                Password = string.IsNullOrWhiteSpace(_password) ? null : _password
            });
            Dialog.Close(K7DialogResult.Ok(user));
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
