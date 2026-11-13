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

    private const string BasePath = "/settings/scrobbling";

    private bool _loading = true;
    private bool _canScrobble;
    private List<UserScrobblerAccountDto> _accounts = [];
    private bool _selectionMode;
    private bool _deleting;
    private readonly HashSet<Guid> _selectedIds = [];
    private SelectionModeKeyboardBinder? _selectionKeys;

    /// <summary>Account whose sheet is open. Null shows the list (plus a default sheet on wide screens).</summary>
    [Parameter] public Guid? AccountId { get; set; }

    /// <summary>The account from the address, else the first of the list.</summary>
    private UserScrobblerAccountDto? SelectedAccount =>
        _accounts.FirstOrDefault(a => a.Id == AccountId) ?? _accounts.FirstOrDefault();

    private bool IsAccountOpen => AccountId is not null && _accounts.Any(a => a.Id == AccountId);

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

    private static bool IsWebhook(UserScrobblerAccountDto account) =>
        account.Provider == nameof(ScrobblerProvider.Webhook);

    private static string GetProviderIcon(string provider) => provider switch
    {
        nameof(ScrobblerProvider.LastFm) => Phosphor.LastfmLogo,
        nameof(ScrobblerProvider.ListenBrainz) => Phosphor.Headphones,
        nameof(ScrobblerProvider.Trakt) => Phosphor.Television,
        nameof(ScrobblerProvider.Webhook) => Phosphor.WebhooksLogo,
        _ => Phosphor.Broadcast
    };

    private string GetAccountName(UserScrobblerAccountDto account) =>
        string.IsNullOrWhiteSpace(account.DisplayName) ? LocalizeProvider(account.Provider) : account.DisplayName;

    private string LocalizeMediaType(string mediaType) => mediaType switch
    {
        nameof(MediaType.Movie) => L["MediaTypeMovie"],
        nameof(MediaType.SerieEpisode) => L["MediaTypeEpisode"],
        nameof(MediaType.MusicTrack) => L["MediaTypeMusic"],
        _ => mediaType
    };

    private string GetMediaTypeNames(UserScrobblerAccountDto account) =>
        string.Join(", ", account.MediaTypes.Select(LocalizeMediaType));

    /// <summary>
    /// Host of the webhook address. The full address is not shown: it often carries a secret token.
    /// </summary>
    private static string? GetDestination(UserScrobblerAccountDto account) =>
        IsWebhook(account)
        && Uri.TryCreate(account.Url?.Trim(), UriKind.Absolute, out var uri)
        && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host
            : null;

    /// <summary>Second line of the list: the service when the account has its own name, then what it receives.</summary>
    private string GetAccountSummary(UserScrobblerAccountDto account)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(account.DisplayName))
            parts.Add(LocalizeProvider(account.Provider));

        var destination = GetDestination(account);
        if (destination is not null)
            parts.Add(destination);

        var mediaTypes = GetMediaTypeNames(account);
        if (mediaTypes.Length > 0)
            parts.Add(mediaTypes);

        return string.Join(" - ", parts);
    }

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
        var name = GetAccountName(account);
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
        StateHasChanged();
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
        StateHasChanged();
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
