using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class SkipSegmentOverlay : IDisposable
{
    [Inject] private IPlayerService PlayerService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;

    [Parameter] public Guid? MediaId { get; set; }

    private IReadOnlyList<MediaSegmentDto>? _segments;
    private MediaSegmentDto? _activeSegment;
    private VideoPlayerSettingsDto? _settings;
    private bool _visible;
    private bool _autoSkipped;
    private Guid? _loadedMediaId;

    protected override async Task OnParametersSetAsync()
    {
        if (MediaId is not null && MediaId != _loadedMediaId)
        {
            _loadedMediaId = MediaId;
            _autoSkipped = false;
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
        }

        if (active is not null)
        {
            var behavior = active.Type == K7.Shared.Enums.MediaSegmentType.Intro
                ? _settings.IntroSkipBehavior
                : _settings.OutroSkipBehavior;

            if (behavior == IntroSkipBehavior.AutoSkip && !_autoSkipped)
            {
                _autoSkipped = true;
                PlayerService.Seek(active.EndMs / 1000.0);
                _visible = false;
            }
            else if (behavior == IntroSkipBehavior.ShowButton)
            {
                _visible = true;
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

    private void SkipSegment()
    {
        if (_activeSegment is null)
            return;

        PlayerService.Seek(_activeSegment.EndMs / 1000.0);
        _visible = false;
    }

    public void Dispose()
    {
        PlayerService.CurrentTimeChanged -= OnTimeChanged;
    }
}
