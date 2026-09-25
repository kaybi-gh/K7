using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using Microsoft.Maui.Controls.Shapes;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Next-episode offer overlay: countdown autoplay, replay, still images for the current and
/// next episode, and the same StartTracking/PlayIndexedFileAsync path as
/// <c>NextEpisodeOverlay.razor(.cs)</c>.
/// </summary>
public sealed partial class NativeVideoPlayerOverlay
{
    private const int AutoPlayCountdownSeconds = 15;

    private readonly Grid _nextEpisodeOverlay = new();
    private readonly Image _nepCurrentStill = new() { Aspect = Aspect.AspectFill };
    private readonly Image _nepNextStill = new() { Aspect = Aspect.AspectFill };
    private readonly Label _nepCurrentInfo = new();
    private readonly Label _nepNextInfo = new();
    private readonly Label _nepCountdownLabel = new();
    private readonly ProgressBar _nepProgressBar = new();
    private readonly VerticalStackLayout _nepAutoplayFooter = new();

    private LiteSerieEpisodeDto? _nextEpisode;
    private LiteSerieEpisodeDto? _nepCurrentEpisode;
    private string _nepBehavior = "AutoPlay";
    private int _nepCountdownSeconds;
    private int _nepCountdownDuration;
    /// <summary>Bumped to cancel pending still-load / countdown loops.</summary>
    private int _nepOfferEpoch;
    /// <summary>
    /// True from Play Next until the successor reaches Playing. Blocks a second NEP from a
    /// spurious Ended during Android Exo source replace (OK would otherwise advance again).
    /// </summary>
    private bool _advancingToNextEpisode;
    private Guid? _endedEpisodeIdForOffer;

    private Border? _nepReplayCard;
    private Border? _nepPlayCard;
    private Border? _nepDismissButton;
    private int _nepFocusIndex; // 0=replay, 1=play, 2=dismiss
    private int _nepLastCardFocus = 1; // remembered when moving down to Dismiss

    /// <summary>True while the next-episode offer covers the player (TV focus + touch owner).</summary>
    internal bool IsNextEpisodeOfferVisible => _nextEpisodeOverlay.IsVisible;

    /// <summary>True while a dialog-style input modal blocks chrome underneath.</summary>
    internal bool IsInputModalActive => _inputModalActive;

    private bool IsNextEpisodeVisible => _nextEpisodeOverlay.IsVisible;

    private void BuildNextEpisodeOverlay()
    {
        // Full-bleed dim (same as video / chrome scrim); do not inset for safe area.
        _nextEpisodeOverlay.SafeAreaEdges = SafeAreaEdges.None;
        _nextEpisodeOverlay.BackgroundColor = Color.FromArgb("#E6000000");
        _nextEpisodeOverlay.IsVisible = false;
        _nextEpisodeOverlay.InputTransparent = false;
        _nextEpisodeOverlay.CascadeInputTransparent = false;
        _nextEpisodeOverlay.ZIndex = 20;
        _nextEpisodeOverlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        _nextEpisodeOverlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _nextEpisodeOverlay.Padding = new Thickness(24);

        // Do not put a TapGestureRecognizer on the root Grid: on Android it steals taps from
        // child buttons/cards. Pause countdown from explicit control interactions instead.

        var cards = new HorizontalStackLayout { Spacing = 16, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

        _nepReplayCard = BuildNepCard(_nepCurrentStill, _nepCurrentInfo, NativeStrings.Replay, NativePlayerGlyphs.Rewind, () => _ = ReplayCurrentEpisodeAsync(), primary: false);
        _nepPlayCard = BuildNepCard(_nepNextStill, _nepNextInfo, NativeStrings.PlayNow, NativePlayerGlyphs.Play, () => _ = PlayNextEpisodeAsync(), primary: true);
        cards.Children.Add(_nepReplayCard);
        cards.Children.Add(_nepPlayCard);
        Grid.SetRow(cards, 0);
        _nextEpisodeOverlay.Children.Add(cards);

        _nepProgressBar.ProgressColor = NativeOverlayTheme.Accent;
        _nepCountdownLabel.TextColor = Colors.White;
        _nepCountdownLabel.FontSize = 13;
        _nepCountdownLabel.HorizontalTextAlignment = TextAlignment.Center;
        _nepAutoplayFooter.Spacing = 6;
        _nepAutoplayFooter.HorizontalOptions = LayoutOptions.Center;
        _nepAutoplayFooter.WidthRequest = 260;
        _nepAutoplayFooter.IsVisible = false;
        _nepAutoplayFooter.Children.Add(_nepProgressBar);
        _nepAutoplayFooter.Children.Add(_nepCountdownLabel);

        _nepDismissButton = new Border
        {
            Stroke = NativeOverlayTheme.OnMediaActionBorder,
            StrokeThickness = 2,
            BackgroundColor = NativeOverlayTheme.OnMediaAction,
            Padding = new Thickness(16, 10),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 12, 0, 0),
            Content = new Label
            {
                Text = NativeStrings.Dismiss,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            },
            StrokeShape = new RoundRectangle { CornerRadius = 8 }
        };
        var dismissTap = new TapGestureRecognizer();
        dismissTap.Tapped += (_, _) => _ = DismissNextEpisodeAsync();
        _nepDismissButton.GestureRecognizers.Add(dismissTap);

        var footerStack = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        footerStack.Children.Add(_nepAutoplayFooter);
        footerStack.Children.Add(_nepDismissButton);
        Grid.SetRow(footerStack, 1);
        _nextEpisodeOverlay.Children.Add(footerStack);

        Children.Add(_nextEpisodeOverlay);
    }

    private static Border BuildNepCard(Image still, Label info, string actionText, string actionIcon, Action onAction, bool primary)
    {
        var actionFg = primary ? NativeOverlayTheme.OnAccent : Colors.White;
        var content = NativeIconText.CreateContent(actionIcon, actionText, fontSize: 14);
        foreach (var child in content.Children)
        {
            if (child is Label label)
                label.TextColor = actionFg;
        }

        var actionButton = new Border
        {
            Stroke = primary ? Colors.Transparent : NativeOverlayTheme.OnMediaActionBorder,
            StrokeThickness = primary ? 0 : 2,
            BackgroundColor = primary ? NativeOverlayTheme.Accent : NativeOverlayTheme.OnMediaAction,
            Padding = new Thickness(16, 10),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = content,
            StrokeShape = new RoundRectangle { CornerRadius = 8 }
        };
        // Real Button.Clicked is more reliable on Android than a root-level TapGestureRecognizer
        // competing with siblings; the offer root no longer hosts a steal-all tap.
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => onAction();
        actionButton.GestureRecognizers.Add(tap);

        var stillGrid = new Grid { WidthRequest = 260, HeightRequest = 146 };
        stillGrid.Children.Add(still);
        stillGrid.Children.Add(actionButton);

        info.TextColor = Colors.White;
        info.FontSize = 13;
        info.Margin = new Thickness(0, 6, 0, 0);
        info.HorizontalTextAlignment = TextAlignment.Center;

        var stack = new VerticalStackLayout { Spacing = 0 };
        stack.Children.Add(stillGrid);
        stack.Children.Add(info);

        return new Border
        {
            Stroke = Colors.Transparent,
            StrokeThickness = 2,
            BackgroundColor = Colors.Transparent,
            Content = stack,
            Padding = 4,
            StrokeShape = new RoundRectangle { CornerRadius = 10 }
        };
    }

    /// <summary>TV D-pad while the next-episode offer is up. Returns true when consumed.</summary>
    private bool HandleNextEpisodeKey(string key, bool isKeyUp)
    {
        if (!IsNextEpisodeVisible)
            return _inputModalActive; // swallow while modal latch is still held

        // Media keys: activate focused action (do not restart the finished video under the offer).
        if (key is "space" or "mediaplaypause" or "mediaplay" or "mediapause")
        {
            if (!isKeyUp)
                ActivateNepFocus();
            return true;
        }

        // Key-up: consume so chrome-hidden skip logic never fires under the overlay.
        if (isKeyUp)
            return true;

        PauseNextEpisodeCountdown();

        NativeVideoDebug.Log("NextEpisode key=" + key + " focus=" + _nepFocusIndex);

        var left = key is "arrowleft" or "left" or "dpad_left";
        var right = key is "arrowright" or "right" or "dpad_right";
        var up = key is "arrowup" or "up" or "dpad_up";
        var down = key is "arrowdown" or "down" or "dpad_down";

        // Spatial nav: Replay | Play on a row, Dismiss below. Modal owns all keys - no chrome leak.
        if (down)
        {
            if (_nepFocusIndex <= 1)
                _nepLastCardFocus = _nepFocusIndex;
            SetNepFocusIndex(2);
            return true;
        }

        if (up)
        {
            if (_nepFocusIndex == 2)
                SetNepFocusIndex(_nepLastCardFocus);
            return true;
        }

        if (left)
        {
            if (_nepFocusIndex == 2)
                SetNepFocusIndex(0);
            else
                SetNepFocusIndex(Math.Max(0, _nepFocusIndex - 1));
            if (_nepFocusIndex <= 1)
                _nepLastCardFocus = _nepFocusIndex;
            return true;
        }

        if (right)
        {
            if (_nepFocusIndex == 2)
                SetNepFocusIndex(1);
            else
                SetNepFocusIndex(Math.Min(1, _nepFocusIndex + 1));
            if (_nepFocusIndex <= 1)
                _nepLastCardFocus = _nepFocusIndex;
            return true;
        }

        if (key is "enter" or "select" or "dpadcenter" or "dpad_center")
        {
            ActivateNepFocus();
            return true;
        }

        return true;
    }

    private void SetNepFocusIndex(int index)
    {
        _nepFocusIndex = Math.Clamp(index, 0, 2);
        ApplyNepFocusVisual();
    }

    private void ApplyNepFocusVisual()
    {
        ApplyNepCardFocus(_nepReplayCard, _nepFocusIndex == 0);
        ApplyNepCardFocus(_nepPlayCard, _nepFocusIndex == 1);
        if (_nepDismissButton is not null)
        {
            var focused = _nepFocusIndex == 2;
            _nepDismissButton.StrokeThickness = 2;
            _nepDismissButton.Stroke = focused ? Colors.White : NativeOverlayTheme.OnMediaActionBorder;
            _nepDismissButton.BackgroundColor = focused
                ? NativeOverlayTheme.OnMediaActionFocus
                : NativeOverlayTheme.OnMediaAction;
        }
    }

    private static void ApplyNepCardFocus(Border? card, bool focused)
    {
        if (card is null)
            return;

        // Keep StrokeThickness constant so focus does not resize cards/stills on TV.
        card.StrokeThickness = 2;
        card.Stroke = focused ? Colors.White : Colors.Transparent;
        card.BackgroundColor = focused ? Color.FromArgb("#22FFFFFF") : Colors.Transparent;
    }

    private void ActivateNepFocus()
    {
        switch (_nepFocusIndex)
        {
            case 0:
                _ = ReplayCurrentEpisodeAsync();
                break;
            case 1:
                _ = PlayNextEpisodeAsync();
                break;
            default:
                _ = DismissNextEpisodeAsync();
                break;
        }
    }

    private void PauseNextEpisodeCountdown()
    {
        _nepOfferEpoch++;
    }

    private void StopNepCountdownTimer()
    {
        // RunNepCountdownAsync watches _nepOfferEpoch; bumping cancels the loop.
        _nepOfferEpoch++;
    }

    /// <summary>
    /// Shows the next-episode offer when this is a series episode with a successor.
    /// Returns false when there is nothing to offer (movie, last episode, or behavior Off)
    /// so the player can close.
    /// </summary>
    private async Task<bool> TryLoadNextEpisodeOfferAsync()
    {
        if (_mediaService is null || _progressTracker is null)
            return false;

        if (_nepBehavior == "Off" || _advancingToNextEpisode)
            return false;

        var serieId = _progressTracker.CurrentSerieId;
        // Prefer the episode that actually ended. After Play Next the tracker may already
        // point at the successor - resolving next from that id offers ep+2 (1 to 3).
        var episodeId = _endedEpisodeIdForOffer
            ?? _progressTracker.CurrentMediaId
            ?? _player.Source?.MediaId;
        if (serieId is null || episodeId is null)
            return false;

        try
        {
            var currentMedia = await _mediaService.GetMediaAsync(episodeId.Value);
            if (currentMedia is SerieEpisodeDto currentDto)
            {
                _nepCurrentEpisode = new LiteSerieEpisodeDto
                {
                    Id = currentDto.Id,
                    Title = currentDto.Title,
                    EpisodeNumber = currentDto.EpisodeNumber,
                    SeasonNumber = currentDto.SeasonNumber,
                    Pictures = currentDto.Pictures
                };
                _nepCurrentInfo.Text = FormatEpisodeLabel(_nepCurrentEpisode);
            }
        }
        catch
        {
            // Best-effort current-episode still.
        }

        if (_advancingToNextEpisode || IsStaleNextEpisodeOffer(episodeId.Value))
            return false;

        try
        {
            _nextEpisode = await _mediaService.GetNextEpisodeAsync(serieId.Value, episodeId.Value);
        }
        catch
        {
            _nextEpisode = null;
        }

        if (_nextEpisode is null)
            return false;

        if (_advancingToNextEpisode || IsStaleNextEpisodeOffer(episodeId.Value))
        {
            _nextEpisode = null;
            return false;
        }

        _nepNextInfo.Text = FormatEpisodeLabel(_nextEpisode);

        // Capture URLs now; assign Image.Source after chrome + countdown are up so still
        // decode cannot race the first countdown paints (Card/Medium, not Hero original).
        var currentStillUrl = GetStillUrl(_nepCurrentEpisode?.Pictures);
        var nextStillUrl = GetStillUrl(_nextEpisode.Pictures);

        void ShowOffer()
        {
            if (_advancingToNextEpisode || IsStaleNextEpisodeOffer(episodeId.Value))
                return;

            // Make the NEP child visible BEFORE SetInputModalActive. That path runs
            // SyncTvSurfaceComposition, which keeps the Android overlay GONE unless
            // IsNextEpisodeVisible is already true (otherwise countdown ticks while the
            // layer is off-screen and the user first sees ~10).
            _nepCurrentStill.Source = null;
            _nepNextStill.Source = null;
            _nextEpisodeOverlay.IsVisible = true;
            _nextEpisodeOverlay.ZIndex = 100;
            SetInputModalActive(true);
            SyncTvSurfaceComposition();
            _nepLastCardFocus = 1;
            SetNepFocusIndex(1); // Default TV focus on Play Next

            if (_nepBehavior == "AutoPlay")
            {
                _nepAutoplayFooter.IsVisible = true;
                _nepCountdownDuration = AutoPlayCountdownSeconds;
                _nepCountdownSeconds = AutoPlayCountdownSeconds;
                UpdateNepCountdownUi();
                _ = RunNepCountdownAsync(AutoPlayCountdownSeconds);
            }
            else
                _nepAutoplayFooter.IsVisible = false;

            NativeVideoDebug.Log("NextEpisode modal open focus=Play");
            _ = LoadNepStillsAsync(currentStillUrl, nextStillUrl);
        }

        if (MainThread.IsMainThread)
            ShowOffer();
        else
            MainThread.BeginInvokeOnMainThread(ShowOffer);

        return true;
    }

    /// <summary>
    /// True when playback already moved past the ended episode (Play Next in flight or
    /// successor source bound). Showing NEP then would overlay the wrong title.
    /// </summary>
    private bool IsStaleNextEpisodeOffer(Guid endedEpisodeId)
    {
        if (_progressTracker?.CurrentMediaId is Guid tracked && tracked != endedEpisodeId)
            return true;

        if (_player.Source?.MediaId is Guid playing && playing != endedEpisodeId
            && _player.PlaybackState is PlaybackState.Playing or PlaybackState.Buffering)
            return true;

        return false;
    }

    private string? GetStillUrl(IReadOnlyList<MetadataPictureDto>? pictures)
    {
        // Card-sized stills for 260x146 NEP thumbs (Hero/original is far too heavy on TV).
        var uri = pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)?
            .GetUri(MetadataPictureDisplayHelper.SizeFor(ImageDisplayRole.Card))?.OriginalString;
        return _server?.GetAbsoluteUri(uri)?.AbsoluteUri ?? uri;
    }

    private static string FormatEpisodeLabel(LiteSerieEpisodeDto episode)
    {
        var code = $"S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";
        return string.IsNullOrEmpty(episode.Title) ? code : $"{code} - {episode.Title}";
    }

    private async Task LoadNepStillsAsync(string? currentStillUrl, string? nextStillUrl)
    {
        var epoch = _nepOfferEpoch;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (epoch != _nepOfferEpoch || !IsNextEpisodeVisible)
                    return;

                if (!string.IsNullOrEmpty(currentStillUrl))
                    _nepCurrentStill.Source = currentStillUrl;
                if (!string.IsNullOrEmpty(nextStillUrl))
                    _nepNextStill.Source = nextStillUrl;
            });
        }
        catch
        {
            // Best-effort artwork.
        }
    }

    /// <summary>
    /// Show N, wait 1s, show N-1. Never derive the label from wall-clock elapsed time:
    /// Android UI stalls used to skip straight to ~10 before the first paint of 15.
    /// </summary>
    private async Task RunNepCountdownAsync(int seconds)
    {
        var epoch = ++_nepOfferEpoch;
        _nepCountdownDuration = seconds;

        try
        {
            for (var left = seconds; left >= 1; left--)
            {
                if (epoch != _nepOfferEpoch)
                    return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (epoch != _nepOfferEpoch || !IsNextEpisodeVisible)
                        return;

                    _nepCountdownSeconds = left;
                    _nepAutoplayFooter.IsVisible = true;
                    UpdateNepCountdownUi();
                });

                await Task.Delay(1000).ConfigureAwait(false);
            }

            if (epoch != _nepOfferEpoch)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (epoch != _nepOfferEpoch || !IsNextEpisodeVisible)
                    return;

                _nepCountdownSeconds = 0;
                UpdateNepCountdownUi();
                _ = PlayNextEpisodeAsync();
            });
        }
        catch
        {
            // Epoch bump / dismiss cancels the loop.
        }
    }

    private void UpdateNepCountdownUi()
    {
        _nepCountdownLabel.Text = NativeStrings.AutoPlayIn(_nepCountdownSeconds);
        _nepProgressBar.Progress = _nepCountdownDuration > 0
            ? (double)_nepCountdownSeconds / _nepCountdownDuration
            : 0;
    }

    private Task ReplayCurrentEpisodeAsync()
    {
        PauseNextEpisodeCountdown();
        ResetNextEpisodeState();
        _player.Seek(0);
        _player.Play();
        return Task.CompletedTask;
    }

    private async Task PlayNextEpisodeAsync()
    {
        if (_advancingToNextEpisode
            || _nextEpisode is null
            || _mediaService is null
            || _featureAccess is null
            || _progressTracker is null)
            return;

        _advancingToNextEpisode = true;
        PauseNextEpisodeCountdown();
        var nextEpisodeId = _nextEpisode.Id;
        var serieId = _progressTracker.CurrentSerieId;
        ResetNextEpisodeState();

        _progressTracker.StopTracking();

        try
        {
            var episodeMedia = await _mediaService.GetMediaAsync(nextEpisodeId);
            if (episodeMedia is not SerieEpisodeDto episodeDto)
                return;

            var indexedFile = episodeDto.IndexedFiles?.FirstOrDefault();
            if (indexedFile is null)
                return;

            if (indexedFile.FileMetadata is not VideoFileMetadataDto videoMetadata)
                return;

            _progressTracker.StartTracking(
                nextEpisodeId,
                await _featureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress),
                serieId,
                indexedFile.Id);

            await _player.PlayIndexedFileAsync(
                indexedFile.Id,
                videoMetadata.AudioTracks ?? [],
                videoMetadata.SubtitleTracks,
                PlaybackTrackContinuity.MatchAudioIndex(videoMetadata.AudioTracks, _player.SelectedAudioTrack),
                PlaybackTrackContinuity.MatchSubtitleIndex(videoMetadata.SubtitleTracks, _player.SelectedSubtitleTrack),
                videoMetadata.VideoResolution,
                videoMetadata.Thumbnails?.Uri?.ToString(),
                nextEpisodeId,
                VideoPlayerTitleHelper.FormatEpisode(episodeDto),
                chapters: videoMetadata.Chapters,
                durationSeconds: videoMetadata.Duration.TotalSeconds,
                libraryId: indexedFile.LibraryId,
                filePath: indexedFile.Path);
        }
        finally
        {
            // Keep the latch until Playing/Idle once tracking moved to the successor.
            // Clearing when state is still Ended (Buffering after Ended is ignored) would
            // let a late Exo Ended reopen NEP on top of ep2.
            if (_progressTracker.CurrentMediaId != nextEpisodeId)
                _advancingToNextEpisode = false;
        }
    }

    /// <summary>Internal reset (playback resumed elsewhere) - hides the overlay without closing
    /// the player. Mirrors NextEpisodeOverlay.Reset()/OnPlaybackStateChangedAsync(Playing).</summary>
    private void ResetNextEpisodeState()
    {
        _nextEpisodeOverlay.IsVisible = false;
        _nextEpisode = null;
        _nepCurrentEpisode = null;
        _endedEpisodeIdForOffer = null;
        _pendingPlaybackEndedAfterScrub = false;
        _nepAutoplayFooter.IsVisible = false;
        StopNepCountdownTimer();
        _nepCountdownSeconds = 0;
        _nepCountdownDuration = 0;
        ApplyNepCardFocus(_nepReplayCard, focused: false);
        ApplyNepCardFocus(_nepPlayCard, focused: false);
        if (_nepDismissButton is not null)
        {
            _nepDismissButton.StrokeThickness = 2;
            _nepDismissButton.BackgroundColor = NativeOverlayTheme.OnMediaAction;
            _nepDismissButton.Stroke = NativeOverlayTheme.OnMediaActionBorder;
        }
        SetInputModalActive(false);
        SyncTvSurfaceComposition();
        NativeVideoDebug.Log("NextEpisode modal closed");
    }

    private void DismissNextEpisode() => ResetNextEpisodeState();

    /// <summary>Dismiss button / Back press while the overlay is visible - mirrors
    /// NextEpisodeOverlay.Dismiss(): fully stops and hides the player.</summary>
    private async Task DismissNextEpisodeAsync()
    {
        PauseNextEpisodeCountdown();
        ResetNextEpisodeState();
        _progressTracker?.StopTracking();
        _player.Stop();
        await _player.HideAsync();
    }
}
