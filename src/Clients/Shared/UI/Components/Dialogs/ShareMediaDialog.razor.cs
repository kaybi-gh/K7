using System.Globalization;
using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Shares;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public enum ShareExpirationChoice
{
    Never,
    OneHour,
    OneDay,
    OneWeek,
    OneMonth,
    Custom,

    /// <summary>Edit only: leave the current expiration as it is.</summary>
    Keep
}

/// <summary>
/// Creates a public share link for a media, or edits an existing share.
/// On creation the link is shown once: the server only keeps a hash of it.
/// </summary>
public partial class ShareMediaDialog
{
    [Inject] private IMediaShareService ShareService { get; set; } = default!;
    [Inject] private IK7ServerService ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    /// <summary>Media to share. Ignored when editing.</summary>
    [Parameter] public Guid MediaId { get; set; }

    /// <summary>Type of the media: only a single playable item can close once consumed.</summary>
    [Parameter] public MediaType MediaType { get; set; }

    /// <summary>Share to edit. Null creates a new one.</summary>
    [Parameter] public MediaShareDto? ExistingShare { get; set; }

    private const int MinPasswordLength = 6;

    private string _label = "";
    private string _password = "";
    private bool _removePassword;
    private ShareExpirationChoice _expiration = ShareExpirationChoice.OneWeek;
    private string _customExpiration = "";
    private string _maxAccessCount = "";
    private bool _allowPlayback = true;
    private bool _allowDownload;
    private bool _closeWhenConsumed;
    private bool _isSubmitting;
    private string? _error;
    private string? _createdLink;

    private bool IsEdit => ExistingShare is not null;

    private MediaType EffectiveMediaType => ExistingShare?.MediaType ?? MediaType;

    private bool CanCloseWhenConsumed =>
        EffectiveMediaType is MediaType.Movie or MediaType.SerieEpisode or MediaType.MusicTrack;

    private string PasswordHint => IsEdit && ExistingShare!.HasPassword
        ? L["PasswordHintKeep"]
        : L["PasswordHint"];

    private IEnumerable<ShareExpirationChoice> ExpirationChoices
    {
        get
        {
            if (IsEdit)
                yield return ShareExpirationChoice.Keep;

            yield return ShareExpirationChoice.OneHour;
            yield return ShareExpirationChoice.OneDay;
            yield return ShareExpirationChoice.OneWeek;
            yield return ShareExpirationChoice.OneMonth;
            yield return ShareExpirationChoice.Custom;
            yield return ShareExpirationChoice.Never;
        }
    }

    protected override void OnInitialized()
    {
        if (ExistingShare is not { } share)
            return;

        _label = share.Label ?? "";
        _expiration = ShareExpirationChoice.Keep;
        _maxAccessCount = share.MaxAccessCount?.ToString(CultureInfo.InvariantCulture) ?? "";
        _allowPlayback = share.AllowPlayback;
        _allowDownload = share.AllowDownload;
        _closeWhenConsumed = share.CloseWhenConsumed;
    }

    private string GetExpirationLabel(ShareExpirationChoice choice) => choice switch
    {
        ShareExpirationChoice.Keep => ExistingShare?.ExpiresAt is { } expiresAt
            ? string.Format(L["ExpirationKeep"], expiresAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))
            : L["ExpirationKeepNever"],
        ShareExpirationChoice.OneHour => L["ExpirationOneHour"],
        ShareExpirationChoice.OneDay => L["ExpirationOneDay"],
        ShareExpirationChoice.OneWeek => L["ExpirationOneWeek"],
        ShareExpirationChoice.OneMonth => L["ExpirationOneMonth"],
        ShareExpirationChoice.Custom => L["ExpirationCustom"],
        _ => L["ExpirationNever"]
    };

    private void Cancel() => Dialog.Cancel();

    private void Close() => Dialog.Close(K7DialogResult.Ok(true));

    private async Task Submit()
    {
        _error = null;

        if (!_allowPlayback && !_allowDownload)
        {
            _error = L["ErrorNothingAllowed"];
            return;
        }

        if (!TryReadMaxAccessCount(out var maxAccessCount))
        {
            _error = L["ErrorMaxAccess"];
            return;
        }

        if (!TryReadExpiration(out var expiresAt))
        {
            _error = L["ErrorExpiration"];
            return;
        }

        if (_password.Length is > 0 and < MinPasswordLength)
        {
            _error = L["ErrorPassword"];
            return;
        }

        _isSubmitting = true;
        try
        {
            if (ExistingShare is { } share)
            {
                await ShareService.UpdateMediaShareAsync(share.Id, new UpdateMediaShareRequest
                {
                    Label = _label,
                    Password = _removePassword ? "" : _password.Length > 0 ? _password : null,
                    ExpiresAt = expiresAt,
                    MaxAccessCount = maxAccessCount,
                    AllowPlayback = _allowPlayback,
                    AllowDownload = _allowDownload,
                    CloseWhenConsumed = CanCloseWhenConsumed && _closeWhenConsumed
                });
                Close();
                return;
            }

            var link = await ShareService.CreateMediaShareAsync(new CreateMediaShareRequest
            {
                MediaId = MediaId,
                Label = _label,
                Password = _password.Length > 0 ? _password : null,
                ExpiresAt = expiresAt,
                MaxAccessCount = maxAccessCount,
                AllowPlayback = _allowPlayback,
                AllowDownload = _allowDownload,
                CloseWhenConsumed = CanCloseWhenConsumed && _closeWhenConsumed
            });

            _createdLink = ShareLinkBuilder.Build(ServerService, link.Token);
        }
        catch (Exception)
        {
            _error = L["ErrorSave"];
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private bool TryReadMaxAccessCount(out int? maxAccessCount)
    {
        maxAccessCount = null;
        if (string.IsNullOrWhiteSpace(_maxAccessCount))
            return true;

        if (!int.TryParse(_maxAccessCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 1)
            return false;

        maxAccessCount = value;
        return true;
    }

    private bool TryReadExpiration(out DateTimeOffset? expiresAt)
    {
        var now = DateTimeOffset.UtcNow;
        expiresAt = _expiration switch
        {
            ShareExpirationChoice.Keep => ExistingShare?.ExpiresAt,
            ShareExpirationChoice.OneHour => now.AddHours(1),
            ShareExpirationChoice.OneDay => now.AddDays(1),
            ShareExpirationChoice.OneWeek => now.AddDays(7),
            ShareExpirationChoice.OneMonth => now.AddDays(30),
            _ => null
        };

        if (_expiration != ShareExpirationChoice.Custom)
            return true;

        // datetime-local gives a local date and time without offset.
        if (!DateTime.TryParse(_customExpiration, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local))
            return false;

        expiresAt = new DateTimeOffset(local).ToUniversalTime();
        return expiresAt > now;
    }

    private async Task CopyLink()
    {
        if (_createdLink is null)
            return;

        await JS.InvokeVoidAsync("K7.shareOrCopy", _createdLink);
        Snackbar.Add(L["LinkCopied"], K7Severity.Success);
    }
}
