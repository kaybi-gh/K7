using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class CreateApiKeyDialog
{
    [Inject] private IApiKeyAdminService ApiKeyAdminService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    private string _name = "";
    private ApiKeyScope _scope = ApiKeyScope.Read;
    private bool _isSubmitting;
    private string? _createdKey;

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(_name)) return;

        _isSubmitting = true;
        try
        {
            var result = await ApiKeyAdminService.CreateApiKeyAsync(_name.Trim(), _scope);
            _createdKey = result.FullKey;
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
        if (_createdKey is null) return;
        await JS.InvokeVoidAsync("K7.shareOrCopy", _createdKey);
        Snackbar.Add(L["Copied"], K7Severity.Success);
    }

    private void Close() => Dialog.Close(K7DialogResult.Ok(true));
}
