using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminApiKeysPanel
{
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    private List<ApiKeyDto> _apiKeys = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadKeys();
    }

    private async Task LoadKeys(bool showLoading = true)
    {
        if (showLoading)
            _loading = true;

        try
        {
            _apiKeys = await ApiKeyAdminService.GetApiKeysAsync();
        }
        catch
        {
            _apiKeys = [];
        }

        _loading = false;
    }

    private async Task ShowCreateDialog()
    {
        var parameters = new K7DialogParameters<CreateApiKeyDialog>
        {
            { x => x.OnCreated, EventCallback.Factory.Create(this, () => LoadKeys(showLoading: false)) }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreateApiKeyDialog>(L["CreateKey"], parameters, options);
        await dialog.Result;

        // Refresh even if the dialog was dismissed via X/Escape after a successful create.
        await LoadKeys(showLoading: false);
    }

    private async Task RevokeKey(ApiKeyDto key)
    {
        var parameters = new K7DialogParameters<ConfirmDeleteUserDialog>
        {
            { x => x.DisplayName, key.Name }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteUserDialog>(L["RevokeConfirmTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                await ApiKeyAdminService.RevokeApiKeyAsync(key.Id);
                await LoadKeys();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }
}
