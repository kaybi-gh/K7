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
    private bool _nepVisible;
    private string _nepBehavior = "AutoPlay";
    private int _nepCountdownSeconds;
    private int _nepCountdownDuration;
    private bool _nepCountdownActive;
    private System.Timers.Timer? _nepCountdownTimer;

    private Border? _nepReplayCard;
    private Border? _nepPlayCard;
    private Button? _nepDismissButton;
    private int _nepFocusIndex; // 0=replay, 1=play, 2=dismiss

    private bool IsNextEpisodeVisible => _nepVisible;

    private void BuildNextEpisodeOverlay()
    {
        _nextEpisodeOverlay.BackgroundColor = Color.FromArgb("#E6000000");
        _nextEpisodeOverlay.IsVisible = false;
        _nextEpisodeOverlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        _nextEpisodeOverlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _nextEpisodeOverlay.Padding = new Thickness(24);

        var activityTap = new TapGestureRecognizer();
        activityTap.Tapped += (_, _) => PauseNextEpisodeCountdown();
        _nextEpisodeOverlay.GestureRecognizers.Add(activityTap);

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

        _nepDismissButton = new Button
        {
            Text = NativeStrings.Dismiss,
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#33FFFFFF"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 12, 0, 0)
        };
        _nepDismissButton.Clicked += (_, _) => _ = DismissNextEpisodeAsync();

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
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
        };
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
            return false;

        // Key-up: consume so chrome-hidden skip logic never fires under the overlay.
        if (isKeyUp)
            return true;

        PauseNextEpisodeCountdown();

        if (key is "arrowleft" or "left" or "dpad_left" or "arrowup" or "up" or "dpad_up")
        {
            SetNepFocusIndex(_nepFocusIndex <= 0 ? 2 : _nepFocusIndex - 1);
            return true;
        }

        if (key is "arrowright" or "right" or "dpad_right" or "arrowdown" or "down" or "dpad_down")
        {
            SetNepFocusIndex(_nepFocusIndex >= 2 ? 0 : _nepFocusIndex + 1);
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
            _nepDismissButton.BackgroundColor = _nepFocusIndex == 2
                ? Color.FromArgb("#66FFFFFF")
                : Color.FromArgb("#33FFFFFF");
            _nepDismissButton.BorderColor = _nepFocusIndex == 2 ? Colors.White : Colors.Transparent;
            _nepDismissButton.BorderWidth = _nepFocusIndex == 2 ? 2 : 0;
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

    private async Task LoadNextEpisodeOfferAsync()
    {
        if (_mediaService is null || _progressTracker is null)
            return;

        if (_nepBehavior == "Off")
            return;

        var serieId = _progressTracker.CurrentSerieId;
        var episodeId = _progressTracker.CurrentMediaId ?? _player.Source?.MediaId;
        if (serieId is null || episodeId is null)
            return;

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
            return;

        _nepNextStill.Source = GetStillUrl(_nextEpisode.Pictures);
        _nepNextInfo.Text = FormatEpisodeLabel(_nextEpisode);

        _nepVisible = true;
        _nextEpisodeOverlay.IsVisible = true;
        SetNepFocusIndex(1); // Default TV focus on Play Next

        if (_nepBehavior == "AutoPlay")
            StartNextEpisodeCountdown(AutoPlayCountdownSeconds);
        else
            _nepAutoplayFooter.IsVisible = false;
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
            videoMetadata.AudioTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata.SubtitleTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata.VideoResolution,
            videoMetadata.Thumbnails?.Uri?.ToString(),
            nextEpisodeId,
            VideoPlayerTitleHelper.FormatEpisode(episodeDto),
            chapters: videoMetadata.Chapters);
    }

    /// <summary>Internal reset (playback resumed elsewhere) - hides the overlay without closing
    /// the player. Mirrors NextEpisodeOverlay.Reset()/OnPlaybackStateChangedAsync(Playing).</summary>
    private void ResetNextEpisodeState()
    {
        _nepVisible = false;
        _nextEpisodeOverlay.IsVisible = false;
        _nextEpisode = null;
        _nepCurrentEpisode = null;
        _nepAutoplayFooter.IsVisible = false;
        StopNepCountdownTimer();
        _nepCountdownActive = false;
        _nepCountdownSeconds = 0;
        _nepCountdownDuration = 0;
    }

    private void DismissNextEpisode() => ResetNextEpisodeState();

    /// <summary>Dismiss button / Back press while the overlay is visible - mirrors
    /// NextEpisodeOverlay.Dismiss(): fully stops and hides the player.</summary>
    private async Task DismissNextEpisodeAsync()
    {
        PauseNextEpisodeCountdown();
        ResetNextEpisodeState();
        _player.Stop();
        await _player.HideAsync();
    }
}
