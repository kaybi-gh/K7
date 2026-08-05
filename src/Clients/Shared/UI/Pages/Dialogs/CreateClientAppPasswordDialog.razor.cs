using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages.Dialogs;

public partial class CreateClientAppPasswordDialog
{
    [Inject] private IClientAppPasswordUserService ClientAppPasswordUserService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    private string _name = "";
    private bool _isSubmitting;
    private string? _createdPassword;
    private CreateClientAppPasswordResponse? _response;

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(_name))
            return;

        _isSubmitting = true;
        try
        {
            _response = await ClientAppPasswordUserService.CreateClientAppPasswordAsync(_name.Trim());
            _createdPassword = _response.Password;
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private async Task CopyToClipboard()
    {
        if (_createdPassword is null)
            return;

        await JS.InvokeVoidAsync("K7.shareOrCopy", _createdPassword);
        Snackbar.Add(L["Copied"], K7Severity.Success);
    }

    private void Close() => Dialog.Close(K7DialogResult.Ok(_response));
}
