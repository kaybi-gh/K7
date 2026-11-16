using System.Globalization;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Shares;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Manages public share links: the list, and the sheet of the selected share with its
/// settings, actions and recent visitors. Used for a user's own shares and, for
/// administrators, for the shares of every user.
/// </summary>
public partial class MediaSharesManager
{
    [Inject] private IMediaShareService ShareService { get; set; } = default!;
    [Inject] private IMediaSharingAccess SharingAccess { get; set; } = default!;
    [Inject] private IK7ServerService ServerService { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferences { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter, EditorRequired] public string Subtitle { get; set; } = "";

    /// <summary>Route of the list. The sheet of a share lives at "{BasePath}/{id}".</summary>
    [Parameter, EditorRequired] public string BasePath { get; set; } = "";

    /// <summary>Share whose sheet is open. Null shows the list (plus a default sheet on wide screens).</summary>
    [Parameter] public Guid? ShareId { get; set; }

    /// <summary>Administrators only: list the shares of every user instead of the current one.</summary>
    [Parameter] public bool ShowAllUsers { get; set; }

    private bool _isLoading = true;
    private List<MediaShareDto>? _shares;
    private MediaShareDto? _selectedDetail;
    private MediaSharingAvailabilityDto? _availability;
    private Guid? _loadedDetailId;
    private bool _isSavingEnabled;

    private List<MediaShareDto> Shares => _shares ?? [];

    /// <summary>The share from the address, else the first of the list.</summary>
    private MediaShareDto? SelectedShare =>
        Shares.FirstOrDefault(s => s.Id == ShareId) ?? Shares.FirstOrDefault();

    private bool IsShareOpen => ShareId is not null && Shares.Any(s => s.Id == ShareId);

    /// <summary>Editing needs the feature on and the capability. Revoking and deleting do not.</summary>
    private bool CanEdit => _availability is { CanCreate: true };

    protected override async Task OnInitializedAsync()
    {
        _availability = await SharingAccess.GetAsync();
        await LoadShares();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_shares is not null)
            await LoadSelectedDetail();
    }

    /// <summary>
    /// Server switch for public shares, shown to administrators. Saved as soon as it is flipped.
    /// Turning it off closes every link at once without deleting any share.
    /// </summary>
    private async Task OnEnabledChangedAsync(bool enabled)
    {
        if (_isSavingEnabled || _availability is null || enabled == _availability.Enabled)
            return;

        _isSavingEnabled = true;
        try
        {
            var current = await ServerPreferences.GetServerFeatureFlagsAsync();
            await ServerPreferences.UpdateServerFeatureFlagsAsync(current with { PublicSharesEnabled = enabled });
            SharingAccess.Invalidate();
            _availability = await SharingAccess.GetAsync();
            await LoadShares();
        }
        catch (Exception)
        {
            Snackbar.Add(L["ErrorAction"], K7Severity.Error);
        }
        finally
        {
            _isSavingEnabled = false;
        }
    }

    private async Task LoadShares()
    {
        // Only the first load swaps the page for a progress bar: a reload keeps the sheet in place.
        _isLoading = _shares is null;
        try
        {
            _shares = await ShareService.GetMediaSharesAsync(all: ShowAllUsers);
        }
        catch (Exception)
        {
            _shares ??= [];
            Snackbar.Add(L["ErrorLoad"], K7Severity.Error);
        }

        _isLoading = false;
        _loadedDetailId = null;
        await LoadSelectedDetail();
    }

    private async Task LoadSelectedDetail()
    {
        var selectedId = SelectedShare?.Id;
        if (selectedId == _loadedDetailId)
            return;

        _loadedDetailId = selectedId;
        _selectedDetail = null;
        if (selectedId is not { } id)
            return;

        try
        {
            var detail = await ShareService.GetMediaShareAsync(id);

            // The selection may have moved while the request was running.
            if (_loadedDetailId == id)
                _selectedDetail = detail;
        }
        catch (Exception)
        {
            _loadedDetailId = null;
        }
    }

    private async Task OpenEditDialog(MediaShareDto share)
    {
        var parameters = new K7DialogParameters
        {
            [nameof(ShareMediaDialog.ExistingShare)] = share
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };

        var dialog = await DialogService.ShowAsync<ShareMediaDialog>(share.MediaTitle, parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadShares();
            Snackbar.Add(L["ShareUpdated"], K7Severity.Success);
        }
    }

    private async Task ConfirmRegenerate(MediaShareDto share)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["NewLinkConfirmTitle"],
            L["NewLinkConfirmMessage"],
            yesText: L["ActionNewLink"],
            noText: S["Cancel"]);

        if (confirmed != true)
            return;

        try
        {
            var link = await ShareService.RegenerateMediaShareLinkAsync(share.Id);
            var parameters = new K7DialogParameters
            {
                [nameof(ShareLinkDialog.Link)] = ShareLinkBuilder.Build(ServerService, link.Token)
            };
            var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
            var dialog = await DialogService.ShowAsync<ShareLinkDialog>(share.MediaTitle, parameters, options);
            await dialog.Result;
            await LoadShares();
        }
        catch (Exception)
        {
            Snackbar.Add(L["ErrorAction"], K7Severity.Error);
        }
    }

    private async Task ConfirmRevoke(MediaShareDto share)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["RevokeConfirmTitle"],
            string.Format(L["RevokeConfirmMessage"], share.MediaTitle),
            yesText: L["ActionRevoke"],
            noText: S["Cancel"]);

        if (confirmed != true)
            return;

        try
        {
            await ShareService.RevokeMediaShareAsync(share.Id);
            await LoadShares();
            Snackbar.Add(L["ShareRevoked"], K7Severity.Success);
        }
        catch (Exception)
        {
            Snackbar.Add(L["ErrorAction"], K7Severity.Error);
        }
    }

    private async Task ConfirmDelete(MediaShareDto share)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteConfirmTitle"],
            string.Format(L["DeleteConfirmMessage"], share.MediaTitle),
            yesText: S["Delete"],
            noText: S["Cancel"]);

        if (confirmed != true)
            return;

        try
        {
            await ShareService.DeleteMediaShareAsync(share.Id);
            if (ShareId == share.Id)
                NavigationManager.NavigateTo(BasePath);

            await LoadShares();
            Snackbar.Add(L["ShareDeleted"], K7Severity.Success);
        }
        catch (Exception)
        {
            Snackbar.Add(L["ErrorAction"], K7Severity.Error);
        }
    }

    private static string GetRowTitle(MediaShareDto share) =>
        string.IsNullOrWhiteSpace(share.Label) ? share.MediaTitle : share.Label;

    private string GetRowSubtitle(MediaShareDto share)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(share.Label))
            parts.Add(share.MediaTitle);
        else if (!string.IsNullOrWhiteSpace(share.MediaSubtitle))
            parts.Add(share.MediaSubtitle);

        parts.Add(GetAccessSummary(share));

        if (ShowAllUsers && !string.IsNullOrWhiteSpace(share.CreatedByName))
            parts.Add(share.CreatedByName);

        return string.Join(" - ", parts);
    }

    private string GetAccessSummary(MediaShareDto share) => share.MaxAccessCount is { } max
        ? string.Format(L["AccessesOfMax"], share.AccessCount, max)
        : string.Format(L["Accesses"], share.AccessCount);

    private string? GetPictureUrl(MediaShareDto share) =>
        string.IsNullOrEmpty(share.PictureUri)
            ? null
            : ServerService.GetAbsoluteUri($"{share.PictureUri}?size=Small")?.AbsoluteUri;

    private string GetSessionSubtitle(MediaShareSessionDto session)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(session.IpAddress))
            parts.Add(session.IpAddress);

        parts.Add(string.Format(L["LastSeen"], FormatDate(session.LastSeenAt)));

        if (!string.IsNullOrWhiteSpace(session.UserAgent))
            parts.Add(session.UserAgent);

        return string.Join(" - ", parts);
    }

    private static string FormatDate(DateTimeOffset date) =>
        date.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private static string GetStatusColor(MediaShareStatus status) => status switch
    {
        MediaShareStatus.Active => "success",
        MediaShareStatus.Revoked => "error",
        MediaShareStatus.Suspended => "warning",
        _ => "default"
    };

    private string GetStatusLabel(MediaShareStatus status) => status switch
    {
        MediaShareStatus.Active => L["StatusActive"],
        MediaShareStatus.Expired => L["StatusExpired"],
        MediaShareStatus.Exhausted => L["StatusExhausted"],
        MediaShareStatus.Consumed => L["StatusConsumed"],
        MediaShareStatus.Revoked => L["StatusRevoked"],
        _ => L["StatusSuspended"]
    };

    private string? GetStatusHint(MediaShareStatus status) => status switch
    {
        MediaShareStatus.Exhausted => L["StatusExhaustedHint"].Value,
        MediaShareStatus.Consumed => L["StatusConsumedHint"].Value,
        MediaShareStatus.Suspended => L["StatusSuspendedHint"].Value,
        _ => null
    };

    private static string GetMediaTypeIcon(MediaType type) => type switch
    {
        MediaType.Movie => Phosphor.FilmSlate,
        MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode => Phosphor.Television,
        MediaType.MusicArtist => Phosphor.User,
        _ => Phosphor.MusicNote
    };

    private string GetMediaTypeLabel(MediaType type) => type switch
    {
        MediaType.Movie => L["TypeMovie"],
        MediaType.Serie => L["TypeSerie"],
        MediaType.SerieSeason => L["TypeSeason"],
        MediaType.SerieEpisode => L["TypeEpisode"],
        MediaType.MusicArtist => L["TypeArtist"],
        MediaType.MusicAlbum => L["TypeAlbum"],
        _ => L["TypeTrack"]
    };
}
