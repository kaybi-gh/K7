using System.Net;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components.Shares;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Shares;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages.Shares;

public enum ShareViewState
{
    Loading,
    PasswordRequired,
    Content,
    NotFound,
    Unavailable,
    SessionEnded,
    MediaNotInShare,
    TooManyAttempts,
    Error
}

public partial class SharePage : IDisposable
{
    /// <summary>How often the playback position is sent while playing.</summary>

    [Inject] private IPublicShareService PublicShare { get; set; } = default!;
    [Inject] private ISharePlaybackScope PlaybackScope { get; set; } = default!;
    [Inject] private IPlayerService PlayerService { get; set; } = default!;
    [Inject] private IAudioPlayerService AudioPlayer { get; set; } = default!;
    [Inject] private PlaybackProgressTracker PlaybackProgressTracker { get; set; } = default!;
    [Inject] private IK7ServerService ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    /// <summary>Set on pages inside a share. The landing page has none.</summary>
    [Parameter] public Guid? ShareId { get; set; }

    /// <summary>Media to show inside the share. The landing page shows the share root.</summary>
    [Parameter] public Guid? MediaId { get; set; }

    private ShareViewState _state = ShareViewState.Loading;
    private MediaShareStatus _unavailableStatus;
    private OpenShareResultDto? _session;
    private MediaDto? _media;
    private string _token = "";
    private string _password = "";
    private string? _passwordError;
    private bool _isSubmitting;
    private (Guid? ShareId, Guid? MediaId) _loaded = (Guid.Empty, Guid.Empty);
    private Guid? _playingVideoMediaId;
    private bool _startedPlayback;

    /// <summary>Back to the root of the share, when the visitor is deeper in it.</summary>
    private string? BackHref =>
        _session is { ShareId: { } shareId, RootMediaId: { } rootMediaId } && _media is not null && rootMediaId != _media.Id
            ? SharePaths.Media(shareId, rootMediaId)
            : null;

    private string StateIcon => _state switch
    {
        ShareViewState.Unavailable => Phosphor.ClockCountdown,
        ShareViewState.SessionEnded => Phosphor.ClockCountdown,
        ShareViewState.TooManyAttempts => Phosphor.Prohibit,
        ShareViewState.Error => Phosphor.WarningCircle,
        _ => Phosphor.LinkBreak
    };

    private string StateTitle => _state switch
    {
        ShareViewState.Unavailable => _unavailableStatus switch
        {
            MediaShareStatus.Expired => L["ExpiredTitle"],
            MediaShareStatus.Exhausted => L["ExhaustedTitle"],
            MediaShareStatus.Consumed => L["ConsumedTitle"],
            _ => L["ClosedTitle"]
        },
        ShareViewState.SessionEnded => L["SessionEndedTitle"],
        ShareViewState.MediaNotInShare => L["MediaNotInShareTitle"],
        ShareViewState.TooManyAttempts => L["TooManyAttemptsTitle"],
        ShareViewState.Error => L["ErrorTitle"],
        _ => L["NotFoundTitle"]
    };

    private string StateText => _state switch
    {
        ShareViewState.Unavailable => _unavailableStatus switch
        {
            MediaShareStatus.Expired => L["ExpiredText"],
            MediaShareStatus.Exhausted => L["ExhaustedText"],
            MediaShareStatus.Consumed => L["ConsumedText"],
            _ => L["ClosedText"]
        },
        ShareViewState.SessionEnded => L["SessionEndedText"],
        ShareViewState.MediaNotInShare => L["MediaNotInShareText"],
        ShareViewState.TooManyAttempts => L["TooManyAttemptsText"],
        ShareViewState.Error => L["ErrorText"],
        _ => L["NotFoundText"]
    };

    protected override async Task OnParametersSetAsync()
    {
        // The router reuses this page when moving between media of the same share.
        if (_loaded == (ShareId, MediaId))
            return;

        _loaded = (ShareId, MediaId);
        _media = null;
        _state = ShareViewState.Loading;

        if (ShareId is { } shareId && MediaId is { } mediaId)
            await LoadInsideShareAsync(shareId, mediaId);
        else
            await OpenFromLinkAsync(password: null);
    }

    /// <summary>
    /// The token sits in the URL fragment, which browsers never send to the server.
    /// </summary>
    private string ReadTokenFromFragment()
    {
        var fragment = new Uri(NavigationManager.Uri).Fragment;
        return Uri.UnescapeDataString(fragment.TrimStart('#'));
    }

    private async Task OpenFromLinkAsync(string? password)
    {
        _token = ReadTokenFromFragment();
        if (string.IsNullOrWhiteSpace(_token))
        {
            _state = ShareViewState.NotFound;
            return;
        }

        try
        {
            var result = await PublicShare.OpenAsync(_token, password);
            if (result is null)
            {
                _state = ShareViewState.NotFound;
                return;
            }

            switch (result.Outcome)
            {
                case OpenShareOutcome.Opened when result.ShareId is { } shareId && result.RootMediaId is { } rootMediaId:
                    // Continue on the in-share address and replace this one: the link token
                    // leaves the address bar and the browser history.
                    NavigationManager.NavigateTo(SharePaths.Media(shareId, rootMediaId), replace: true);
                    break;
                case OpenShareOutcome.PasswordLocked:
                    _state = ShareViewState.TooManyAttempts;
                    break;
                case OpenShareOutcome.PasswordRequired:
                    _passwordError = null;
                    _state = ShareViewState.PasswordRequired;
                    break;
                case OpenShareOutcome.InvalidPassword:
                    _passwordError = L["PasswordInvalid"];
                    _state = ShareViewState.PasswordRequired;
                    break;
                default:
                    _unavailableStatus = result.Status;
                    _state = ShareViewState.Unavailable;
                    break;
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _state = ShareViewState.TooManyAttempts;
        }
        catch (HttpRequestException)
        {
            _state = ShareViewState.Error;
        }
    }

    private async Task LoadInsideShareAsync(Guid shareId, Guid mediaId)
    {
        try
        {
            _session = await PublicShare.GetSessionAsync(shareId);
            if (_session is null)
            {
                _state = ShareViewState.SessionEnded;
                return;
            }

            await EnterShareAsync(shareId);
            await LoadMediaAsync(shareId, mediaId);
        }
        catch (HttpRequestException)
        {
            _state = ShareViewState.Error;
        }
    }

    private async Task LoadMediaAsync(Guid shareId, Guid mediaId)
    {
        _media = await PublicShare.GetMediaAsync(shareId, mediaId);
        _state = _media is null ? ShareViewState.MediaNotInShare : ShareViewState.Content;
    }

    /// <summary>
    /// From here the regular players stream through this share. The browser is probed once:
    /// the server uses the result to choose between direct play, remux and transcode.
    /// </summary>
    private async Task EnterShareAsync(Guid shareId)
    {
        if (PlaybackScope.ShareId == shareId)
            return;

        PlaybackScope.Enter(shareId, await ProbeBrowserAsync());
    }

    private async Task<ShareBrowserCapabilities> ProbeBrowserAsync()
    {
        try
        {
            var tokens = (await JS.InvokeAsync<string[]>("getSupportedVideoProfilesAsync") ?? []).ToList();
            var channels = await JS.InvokeAsync<int>("getAudioOutputChannelsAsync");
            if (channels > 0)
                tokens.Add($"achannels:{channels}");

            return new ShareBrowserCapabilities
            {
                SupportedContainers = await JS.InvokeAsync<string[]>("getSupportedContainersAsync") ?? [],
                SupportedAudioCodecs = await JS.InvokeAsync<string[]>("getSupportedAudioCodecsAsync") ?? [],
                SupportedVideoCodecs = await JS.InvokeAsync<string[]>("getSupportedVideoCodecsAsync") ?? [],
                CapabilityTokens = tokens,
                SupportsHdr = await JS.InvokeAsync<bool>("getHdrSupport"),
                DisplayResolutionWidth = await JS.InvokeAsync<int>("getDisplayResolutionWidth"),
                DisplayResolutionHeight = await JS.InvokeAsync<int>("getDisplayResolutionHeight")
            };
        }
        catch (JSException)
        {
            // Unknown capabilities: the server falls back to a stream every browser plays.
            return new ShareBrowserCapabilities();
        }
    }

    private async Task OnPasswordKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter" || args.Code is "Enter" or "NumpadEnter")
            await SubmitPasswordAsync();
    }

    private async Task SubmitPasswordAsync()
    {
        if (_isSubmitting || string.IsNullOrEmpty(_password))
            return;

        _isSubmitting = true;
        try
        {
            await OpenFromLinkAsync(_password);
        }
        finally
        {
            _isSubmitting = false;
            _password = "";
        }
    }

    /// <summary>
    /// Plays a movie or an episode in the regular video player: audio tracks, subtitles,
    /// quality and speed work as for a signed-in user.
    /// </summary>
    private async Task OnPlayVideoAsync(ShareVideoPlayRequest request)
    {
        if (_session?.ShareId is not { } shareId)
            return;

        try
        {
            // An episode started from its season row is not loaded yet.
            var media = _media?.Id == request.MediaId
                ? _media
                : await PublicShare.GetMediaAsync(shareId, request.MediaId);

            var file = request.IndexedFileId is { } fileId
                ? media?.IndexedFiles?.FirstOrDefault(f => f.Id == fileId)
                : media?.IndexedFiles?.FirstOrDefault();

            if (media is null || file?.FileMetadata is not VideoFileMetadataDto video)
            {
                Snackbar.Add(S["MediaPreparingPlayback"], K7Severity.Info);
                return;
            }

            AudioPlayer.Stop();
            _startedPlayback = true;
            _playingVideoMediaId = media.Id;

            // A visitor has no account: the tracker reports through the share, so the
            // stream shows up for administrators (live streams, playback history).
            PlaybackProgressTracker.StartTracking(media.Id, isAuthenticated: false, indexedFileId: file.Id);

            var coverUrl = ServerService.GetAbsoluteUri(
                media.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                    .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;

            await PlayerService.PlayIndexedFileAsync(
                file.Id,
                video.AudioTracks ?? [],
                video.SubtitleTracks,
                videoResolution: video.VideoResolution,
                thumbnailsUrl: video.Thumbnails?.Uri?.ToString(),
                mediaId: media.Id,
                title: VideoPlayerTitleHelper.FormatFromMedia(media),
                coverUrl: coverUrl,
                chapters: video.Chapters,
                durationSeconds: video.Duration.TotalSeconds,
                originalLanguage: (media as MovieDto)?.OriginalLanguage);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
        {
            _state = ShareViewState.SessionEnded;
        }
        catch (Exception ex) when (PlaybackErrorHelper.IsMediaNotReady(ex))
        {
            Snackbar.Add(S["MediaPreparingPlayback"], K7Severity.Info);
        }
        catch (HttpRequestException)
        {
            Snackbar.Add(L["PlaybackError"], K7Severity.Error);
        }
    }

    /// <summary>
    /// Plays tracks in the regular music player. The queue is exactly what the page handed
    /// over: shared tracks only.
    /// </summary>
    private async Task OnPlayAudioAsync(ShareAudioPlayRequest request)
    {
        try
        {
            _startedPlayback = true;
            _playingVideoMediaId = null;
            await AudioPlayer.PlayTracksAsync(request.Queue, request.StartIndex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
        {
            _state = ShareViewState.SessionEnded;
        }
        catch (HttpRequestException)
        {
            Snackbar.Add(L["PlaybackError"], K7Severity.Error);
        }
    }

    public void Dispose()
    {
        // Leaving the share: nothing keeps playing through it, and the regular client
        // talks to the server again.
        if (_startedPlayback)
        {
            // Last report while the share is still open.
            PlaybackProgressTracker.StopTracking();
            PlayerService.Stop();
            AudioPlayer.Stop();
        }

        PlaybackScope.Exit();
        GC.SuppressFinalize(this);
    }
}
