using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class SkipSegmentOverlay : IDisposable
{
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(5);

    [Inject] private IPlayerService PlayerService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;

    [Parameter] public Guid? MediaId { get; set; }
    [Parameter] public bool ControlsVisible { get; set; }

    public bool CanSkip => _visible && _activeSegment is not null;

    private IReadOnlyList<MediaSegmentDto>? _segments;
    private MediaSegmentDto? _activeSegment;
    private VideoPlayerSettingsDto? _settings;
    private bool _visible;
    private bool _autoSkipped;
    private bool _dismissed;
    private bool _showSkippedNotification;
    private K7.Shared.Enums.MediaSegmentType _skippedSegmentType;
    private CancellationTokenSource? _notificationCts;
    private DateTime _lastSkipTime;
    private DateTime _showTime;
    private Guid? _loadedMediaId;

    protected override async Task OnParametersSetAsync()
    {
        if (MediaId is not null && MediaId != _loadedMediaId)
        {
            _loadedMediaId = MediaId;
            _autoSkipped = false;
            _dismissed = false;
            _activeSegment = null;
            _visible = false;

            try
            {
                _segments = await MediaService.GetMediaSegmentsAsync(MediaId.Value);
                _settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            }
            catch
            {
                _segments = null;
                _settings = null;
            }
        }
    }

    protected override void OnInitialized()
    {
        PlayerService.CurrentTimeChanged += OnTimeChanged;
    }

    private void OnTimeChanged(double currentTimeSeconds)
    {
        if (_segments is null || _segments.Count == 0 || _settings is null)
            return;

        var currentMs = (long)(currentTimeSeconds * 1000);
        MediaSegmentDto? active = null;

        foreach (var segment in _segments)
        {
            if (currentMs >= segment.StartMs && currentMs <= segment.EndMs)
            {
                active = segment;
                break;
            }
        }

        if (active != _activeSegment)
        {
            _activeSegment = active;
            _autoSkipped = false;
            _dismissed = false;
        }

        if (active is not null)
        {
            var behavior = active.Type == K7.Shared.Enums.MediaSegmentType.Intro
                ? _settings.IntroSkipBehavior
                : _settings.OutroSkipBehavior;

            var inCooldown = (DateTime.UtcNow - _lastSkipTime).TotalSeconds < 3;

            if (behavior == IntroSkipBehavior.AutoSkip && !_autoSkipped && !inCooldown)
            {
                _autoSkipped = true;
                _lastSkipTime = DateTime.UtcNow;
                PlayerService.Seek(active.EndMs / 1000.0);
                _visible = false;
                ShowSkippedNotification(active.Type);
            }
            else if (behavior == IntroSkipBehavior.ShowButton && (!_dismissed || ControlsVisible))
            {
                if (!_visible)
                {
                    _showTime = DateTime.UtcNow;
                    _visible = true;
                }
                else if (!ControlsVisible && (DateTime.UtcNow - _showTime) >= DisplayDuration)
                {
                    _visible = false;
                    _dismissed = true;
                }
            }
            else
            {
                _visible = false;
            }
        }
        else
        {
            _visible = false;
        }

        InvokeAsync(StateHasChanged);
    }

    public void SkipSegment()
    {
        if (_activeSegment is null)
            return;

        _lastSkipTime = DateTime.UtcNow;
        PlayerService.Seek(_activeSegment.EndMs / 1000.0);
        _visible = false;
    }

    public void Dispose()
    {
        _notificationCts?.Cancel();
        _notificationCts?.Dispose();
        PlayerService.CurrentTimeChanged -= OnTimeChanged;
    }

    private void ShowSkippedNotification(K7.Shared.Enums.MediaSegmentType type)
    {
        _notificationCts?.Cancel();
        _notificationCts?.Dispose();
        _notificationCts = new CancellationTokenSource();

        _skippedSegmentType = type;
        _showSkippedNotification = true;

        var ct = _notificationCts.Token;
        _ = HideNotificationAfterDelayAsync(ct);
    }

    private async Task HideNotificationAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(3000, ct);
            _showSkippedNotification = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (TaskCanceledException)
        {
        }
    }
}
