using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class ChangePasswordDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public bool HasPassword { get; set; }
    [Parameter] public bool CanRemovePassword { get; set; }

    private bool _hasPassword;
    private bool _canRemovePassword;
    private string _currentPassword = "";
    private string _newPassword = "";
    private string _confirmPassword = "";
    private string? _error;

    protected override void OnParametersSet()
    {
        _hasPassword = HasPassword;
        _canRemovePassword = CanRemovePassword;
    }

    private bool CanSubmit =>
        !string.IsNullOrWhiteSpace(_newPassword) &&
        _newPassword == _confirmPassword &&
        (!_hasPassword || !string.IsNullOrWhiteSpace(_currentPassword));

    private void Submit()
    {
        _error = null;

        if (_newPassword != _confirmPassword)
        {
            _error = L["PasswordMismatch"];
            return;
        }

        var result = new PasswordDialogResult
        {
            Action = _hasPassword ? PasswordAction.Change : PasswordAction.Set,
            CurrentPassword = _hasPassword ? _currentPassword : null,
            NewPassword = _newPassword
        };

        Dialog.Close(K7DialogResult.Ok(result));
    }

    private void RemovePassword()
    {
        var result = new PasswordDialogResult { Action = PasswordAction.Remove };
        Dialog.Close(K7DialogResult.Ok(result));
    }

    private void Cancel() => Dialog.Cancel();
}

public sealed class PasswordDialogResult
{
    public PasswordAction Action { get; init; }
    public string? CurrentPassword { get; init; }
    public string? NewPassword { get; init; }
}

public enum PasswordAction
{
    Change,
    Set,
    Remove
}
