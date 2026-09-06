using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Users;
using K7.Shared.Security;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class CreateUserDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    private string _username = "";
    private string _email = "";
    private string _password = "";
    private string _role = "User";
    private bool _isSubmitting;
    private PasswordPolicyDto _policy = PasswordPolicyDto.Defaults;

    private bool CanSubmit =>
        !string.IsNullOrWhiteSpace(_username)
        && !_isSubmitting
        && (string.IsNullOrWhiteSpace(_password) || PasswordPolicy.IsSatisfiedBy(_password, _policy));

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _policy = await ServerInfoService.GetPasswordPolicyAsync();
        }
        catch
        {
            _policy = PasswordPolicyDto.Defaults;
        }
    }

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        if (!CanSubmit) return;

        _isSubmitting = true;
        try
        {
            var user = await K7ServerService.CreateUserAsync(new CreateUserRequest
            {
                Username = _username.Trim(),
                Role = _role,
                Password = string.IsNullOrWhiteSpace(_password) ? null : _password,
                Email = string.IsNullOrWhiteSpace(_email) ? null : _email.Trim()
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
