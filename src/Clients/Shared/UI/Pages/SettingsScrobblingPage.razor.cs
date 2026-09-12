using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Pages.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Scrobbling;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsScrobblingPage : IAsyncDisposable
{
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    private bool _loading = true;
    private bool _canScrobble;
    private List<UserScrobblerAccountDto> _accounts = [];
    private bool _selectionMode;
    private bool _deleting;
    private readonly HashSet<Guid> _selectedIds = [];
    private SelectionModeKeyboardBinder? _selectionKeys;

    private int SelectedCount => _selectedIds.Count;
    private bool AllSelected => _accounts.Count > 0 && _selectedIds.Count == _accounts.Count;

    protected override async Task OnInitializedAsync()
    {
        _selectionKeys = new SelectionModeKeyboardBinder(
            SpatialNav,
            onEscape: () => _ = InvokeAsync(OnSelectionEscape),
            onSelectAll: () => _ = InvokeAsync(OnSelectionSelectAll));

        _canScrobble = await FeatureAccess.HasCapabilityAsync(Capability.CanScrobble);
        if (!_canScrobble)
        {
            _loading = false;
            return;
        }

        await ReloadAsync();
        _loading = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_selectionKeys is not null)
            await _selectionKeys.DisposeAsync();
    }

    private string LocalizeProvider(string provider) => provider switch
    {
        nameof(ScrobblerProvider.LastFm) => L["ProviderLastFm"],
        nameof(ScrobblerProvider.ListenBrainz) => L["ProviderListenBrainz"],
        nameof(ScrobblerProvider.Trakt) => L["ProviderTrakt"],
        nameof(ScrobblerProvider.Webhook) => L["ProviderWebhook"],
        _ => provider
    };

    private async Task ReloadAsync()
    {
        try
        {
            _accounts = await Scrobbling.GetAccountsAsync();
        }
        catch
        {
            _accounts = [];
        }
    }

    private async Task ShowCreateDialog()
    {
        var parameters = new K7DialogParameters<CreateScrobblerAccountDialog>
        {
            { x => x.OnCreated, EventCallback.Factory.Create(this, ReloadAsync) }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreateScrobblerAccountDialog>(L["CreateTitle"], parameters, options);
        await dialog.Result;
        await ReloadAsync();
    }

    private async Task ShowEditDialog(UserScrobblerAccountDto account)
    {
        var parameters = new K7DialogParameters<EditScrobblerAccountDialog>
        {
            { x => x.Account, account },
            { x => x.OnSaved, EventCallback.Factory.Create(this, ReloadAsync) }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<EditScrobblerAccountDialog>(L["EditTitle"], parameters, options);
        await dialog.Result;
        await ReloadAsync();
    }

    private async Task ToggleAsync(UserScrobblerAccountDto account, bool enabled)
    {
        await Scrobbling.UpdateAccountAsync(account.Id, new UpdateUserScrobblerAccountRequest
        {
            IsEnabled = enabled,
            IncludeNowPlaying = account.IncludeNowPlaying,
            DisplayName = account.DisplayName,
            MediaTypes = account.MediaTypes
        });
        await ReloadAsync();
    }

    private async Task TestAsync(UserScrobblerAccountDto account)
    {
        try
        {
            var result = await Scrobbling.TestAccountAsync(account.Id);
            if (result.Success)
                Snackbar.Add(L["TestOk"], K7Severity.Success);
            else
                Snackbar.Add(string.Format(L["TestFailedWithError"], result.Error ?? L["TestFail"]), K7Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
    }

    private async Task ConfirmDeleteAsync(UserScrobblerAccountDto account)
    {
        var name = account.DisplayName ?? LocalizeProvider(account.Provider);
        var result = await DialogService.ShowMessageBoxAsync(
            L["DeleteAccountTitle"],
            string.Format(L["DeleteAccountMessage"], name),
            yesText: S["Delete"],
            cancelText: S["Cancel"]);

        if (result is not true)
            return;

        try
        {
            await Scrobbling.DeleteAccountAsync(account.Id);
            Snackbar.Add(L["AccountDeleted"], K7Severity.Success);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
    }

    private void EnterSelectionMode()
    {
        _selectionMode = true;
        _selectedIds.Clear();
        _ = _selectionKeys?.SetEnabledAsync(true);
    }

    private void ExitSelectionMode()
    {
        if (!_selectionMode)
            return;

        _selectionMode = false;
        _selectedIds.Clear();
        _ = _selectionKeys?.SetEnabledAsync(false);
    }

    private void ToggleSelection(Guid id)
    {
        if (!_selectedIds.Remove(id))
            _selectedIds.Add(id);
    }

    private void ToggleSelectAll()
    {
        if (AllSelected)
            _selectedIds.Clear();
        else
            SelectAll();
    }

    private void SelectAll()
    {
        _selectedIds.Clear();
        foreach (var account in _accounts)
            _selectedIds.Add(account.Id);
    }

    private void OnSelectionEscape()
    {
        if (_deleting)
            return;

        ExitSelectionMode();
    }

    private void OnSelectionSelectAll()
    {
        if (!_selectionMode || _deleting)
            return;

        SelectAll();
    }

    private bool IsSelected(Guid id) => _selectedIds.Contains(id);

    private async Task ConfirmDeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0 || _deleting)
            return;

        var count = _selectedIds.Count;
        var result = await DialogService.ShowMessageBoxAsync(
            L["DeleteSelectedTitle"],
            string.Format(L["DeleteSelectedMessage"], count),
            yesText: S["Delete"],
            cancelText: S["Cancel"]);

        if (result != true)
            return;

        _deleting = true;
        var failed = 0;

        try
        {
            foreach (var id in _selectedIds.ToList())
            {
                try
                {
                    await Scrobbling.DeleteAccountAsync(id);
                }
                catch
                {
                    failed++;
                }
            }
        }
        finally
        {
            _deleting = false;
        }

        ExitSelectionMode();
        await ReloadAsync();

        if (failed == 0)
            Snackbar.Add(string.Format(L["DeleteSelectedSuccess"], count), K7Severity.Success);
        else if (failed == count)
            Snackbar.Add(L["DeleteSelectedError"], K7Severity.Error);
        else
            Snackbar.Add(string.Format(L["DeleteSelectedPartial"], count - failed, failed), K7Severity.Warning);
    }
}
