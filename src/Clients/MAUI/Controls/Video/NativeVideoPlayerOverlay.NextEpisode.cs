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
    private bool _nepCountdownActive;
    private System.Timers.Timer? _nepCountdownTimer;

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

        _nepProgressBar.ProgressColor = Color.FromArgb("#E50914");
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
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#33FFFFFF"),
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
        var actionButton = new Border
        {
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            BackgroundColor = primary ? Color.FromArgb("#E50914") : Color.FromArgb("#33FFFFFF"),
            Padding = new Thickness(16, 10),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = NativeIconText.CreateContent(actionIcon, actionText, fontSize: 14),
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
            StrokeThickness = 0,
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
        ApplyNepCardFocus(_nepDismissButton, _nepFocusIndex == 2);
        if (_nepDismissButton is not null)
        {
            _nepDismissButton.BackgroundColor = _nepFocusIndex == 2
                ? Color.FromArgb("#66FFFFFF")
                : Color.FromArgb("#33FFFFFF");
        }
    }

    private static void ApplyNepCardFocus(Border? card, bool focused)
    {
        if (card is null)
            return;

        card.Stroke = focused ? Colors.White : Colors.Transparent;
        card.StrokeThickness = focused ? 2 : 0;
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
        if (!_nepCountdownActive)
            return;

        _nepCountdownActive = false;
        StopNepCountdownTimer();
    }

    private void StopNepCountdownTimer()
    {
        _nepCountdownTimer?.Stop();
        _nepCountdownTimer?.Dispose();
        _nepCountdownTimer = null;
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

        if (_nepBehavior == "Off")
            return false;

        var serieId = _progressTracker.CurrentSerieId;
        var episodeId = _progressTracker.CurrentMediaId ?? _player.Source?.MediaId;
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
                _nepCurrentStill.Source = GetStillUrl(currentDto.Pictures);
                _nepCurrentInfo.Text = FormatEpisodeLabel(_nepCurrentEpisode);
            }
        }
        catch
        {
            // Best-effort current-episode still.
        }

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

        _nepNextStill.Source = GetStillUrl(_nextEpisode.Pictures);
        _nepNextInfo.Text = FormatEpisodeLabel(_nextEpisode);

        void ShowOffer()
        {
            SetInputModalActive(true);
            _nextEpisodeOverlay.IsVisible = true;
            _nextEpisodeOverlay.ZIndex = 100;
            _nepLastCardFocus = 1;
            SetNepFocusIndex(1); // Default TV focus on Play Next

            if (_nepBehavior == "AutoPlay")
                StartNextEpisodeCountdown(AutoPlayCountdownSeconds);
            else
                _nepAutoplayFooter.IsVisible = false;

            NativeVideoDebug.Log("NextEpisode modal open focus=Play");
        }

        if (MainThread.IsMainThread)
            ShowOffer();
        else
            MainThread.BeginInvokeOnMainThread(ShowOffer);

        return true;
    }

    private string? GetStillUrl(IReadOnlyList<MetadataPictureDto>? pictures)
    {
        var uri = pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)?
            .GetUri(MetadataPictureDisplayHelper.SizeFor(ImageDisplayRole.Hero))?.OriginalString;
        return _server?.GetAbsoluteUri(uri)?.AbsoluteUri ?? uri;
    }

    private static string FormatEpisodeLabel(LiteSerieEpisodeDto episode)
    {
        var code = $"S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";
        return string.IsNullOrEmpty(episode.Title) ? code : $"{code} - {episode.Title}";
    }

    private void StartNextEpisodeCountdown(int seconds)
    {
        _nepCountdownDuration = seconds;
        _nepCountdownSeconds = seconds;
        _nepCountdownActive = true;
        _nepAutoplayFooter.IsVisible = true;
        UpdateNepCountdownUi();
        StopNepCountdownTimer();
        _nepCountdownTimer = new System.Timers.Timer(1000) { AutoReset = true };
        _nepCountdownTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(OnNepCountdownTick);
        _nepCountdownTimer.Start();
    }

    private void OnNepCountdownTick()
    {
        if (!_nepCountdownActive)
            return;

        _nepCountdownSeconds--;
        if (_nepCountdownSeconds <= 0)
        {
            StopNepCountdownTimer();
            _nepCountdownActive = false;
            _ = PlayNextEpisodeAsync();
            return;
        }

        UpdateNepCountdownUi();
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
        if (_nextEpisode is null || _mediaService is null || _featureAccess is null || _progressTracker is null)
            return;

        PauseNextEpisodeCountdown();
        var nextEpisodeId = _nextEpisode.Id;
        var serieId = _progressTracker.CurrentSerieId;
        ResetNextEpisodeState();

        _progressTracker.StopTracking();

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
            durationSeconds: videoMetadata.Duration.TotalSeconds);
    }

    /// <summary>Internal reset (playback resumed elsewhere) - hides the overlay without closing
    /// the player. Mirrors NextEpisodeOverlay.Reset()/OnPlaybackStateChangedAsync(Playing).</summary>
    private void ResetNextEpisodeState()
    {
        _nextEpisodeOverlay.IsVisible = false;
        _nextEpisode = null;
        _nepCurrentEpisode = null;
        _nepAutoplayFooter.IsVisible = false;
        StopNepCountdownTimer();
        _nepCountdownActive = false;
        _nepCountdownSeconds = 0;
        _nepCountdownDuration = 0;
        SetInputModalActive(false);
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
