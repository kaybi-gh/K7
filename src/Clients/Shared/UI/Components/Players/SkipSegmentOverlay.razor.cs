using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class SkipSegmentOverlay : IDisposable
{
    [Inject] private IPlayerService PlayerService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;

    [Parameter] public Guid? MediaId { get; set; }
    [Parameter] public bool ControlsVisible { get; set; }

    public bool CanSkip => _visible && _activeSegment is not null;

    private IReadOnlyList<MediaSegmentDto>? _segments;
    private MediaSegmentDto? _activeSegment;
    private VideoPlayerSettingsDto? _settings;
    private SkipSegmentPresenter.State _skipState;
    private bool _visible;
    private bool _showSkippedNotification;
    private K7.Shared.Enums.MediaSegmentType _skippedSegmentType;
    private CancellationTokenSource? _notificationCts;
    private Guid? _loadedMediaId;

    protected override async Task OnParametersSetAsync()
    {
        if (MediaId is not null && MediaId != _loadedMediaId)
        {
            _loadedMediaId = MediaId;
            _skipState = default;
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

            ApplyCurrentTime(render: true);
        }
    }

    protected override void OnInitialized()
    {
        PlayerService.CurrentTimeChanged += OnTimeChanged;
        PlayerService.PlayerUxSettingsChanged += OnPlayerUxSettingsChanged;
    }

    private void OnPlayerUxSettingsChanged()
    {
        if (PlayerService.VideoPlayerUxSettings is { } settings)
            _settings = settings;
    }

    protected override void OnParametersSet()
    {
        ApplyCurrentTime(render: false);
    }

    private void OnTimeChanged(double currentTimeSeconds) => ApplyTime(currentTimeSeconds, render: true);

    private void ApplyCurrentTime(bool render) => ApplyTime(PlayerService.CurrentTime, render);

    private void ApplyTime(double currentTimeSeconds, bool render)
    {
        var result = SkipSegmentPresenter.Tick(
            _skipState,
            _segments,
            _settings,
            currentTimeSeconds,
            ControlsVisible,
            DateTime.UtcNow);

        if (result.Action == SkipSegmentPresenter.ActionKind.AutoSkip
            && result.State.ActiveSegment is { } segment)
        {
            PlayerService.Seek(segment.EndMs / 1000.0);
            ShowSkippedNotification(segment.Type);
        }

        _skipState = result.State;
        _activeSegment = result.State.ActiveSegment;
        _visible = result.State.Visible;
        if (render)
            _ = InvokeAsync(StateHasChanged);
    }

    public void SkipSegment()
    {
        if (_skipState.ActiveSegment is null)
            return;

        var endSeconds = _skipState.ActiveSegment.EndMs / 1000.0;
        PlayerService.Seek(endSeconds);
        _skipState = _skipState with
        {
            Visible = false,
            ActiveSegment = null,
            LastSkipUtc = DateTime.UtcNow
        };
        _activeSegment = null;
        _visible = false;
    }

    public void Dispose()
    {
        _notificationCts?.Cancel();
        _notificationCts?.Dispose();
        PlayerService.CurrentTimeChanged -= OnTimeChanged;
        PlayerService.PlayerUxSettingsChanged -= OnPlayerUxSettingsChanged;
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
