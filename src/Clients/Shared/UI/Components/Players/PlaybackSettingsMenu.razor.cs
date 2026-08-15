using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class PlaybackSettingsMenu : IDisposable
{
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Inject] private ILogger<PlaybackSettingsMenu> Logger { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    private bool _open;
    private SettingsSection _activeSection = SettingsSection.None;
    private SettingsSection _focusSectionPending = SettingsSection.None;
    private bool _focusRootPending;
    private bool _stackLayerPushed;
    private bool _detailLayerPushed;
    private ElementReference _stackRef;
    private ElementReference _detailRef;
    private DotNetObjectReference<LayerCloseCallback>? _menuCloseRef;
    private DotNetObjectReference<LayerCloseCallback>? _detailCloseRef;
    private volatile bool _disposed;

    private static readonly double[] _playbackSpeedOptions = [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0];
    private static readonly AspectRatioMode[] _aspectRatioOptions = [AspectRatioMode.Fit, AspectRatioMode.Fill, AspectRatioMode.Stretch];

    protected enum SettingsSection
    {
        None,
        Audio,
        Subtitles,
        Quality,
        Speed,
        AspectRatio
    }

    private IReadOnlyList<SettingsSection> VisibleSections
    {
        get
        {
            var sections = new List<SettingsSection>();
            if (PlayerService.AudioTracks.Count > 1)
                sections.Add(SettingsSection.Audio);
            if (PlayerService.SubtitleTracks.Count > 0)
                sections.Add(SettingsSection.Subtitles);
            if (PlayerService.AvailableQualities.Count > 1)
                sections.Add(SettingsSection.Quality);
            sections.Add(SettingsSection.Speed);
            sections.Add(SettingsSection.AspectRatio);
            return sections;
        }
    }

    public bool TryHandleBack()
    {
        if (!_open)
            return false;

        if (_activeSection != SettingsSection.None)
        {
            _activeSection = SettingsSection.None;
            _focusSectionPending = SettingsSection.None;
            _focusRootPending = true;
            RequestStateHasChanged();
            return true;
        }

        CloseAsync().FireAndForget(Logger);
        return true;
    }

    protected override void OnParametersSet()
    {
        if (_open == Open)
            return;

        _open = Open;
        if (!_open)
            _activeSection = SettingsSection.None;
    }

    protected override void OnInitialized()
    {
        _open = Open;
        PlayerService.PlaybackRateChanged += OnPlaybackRateChanged;
        PlayerService.AudioTrackChanged += OnAudioTrackChanged;
        PlayerService.SubtitleTrackChanged += OnSubtitleTrackChanged;
        PlayerService.SubtitleTracksChanged += OnSubtitleTracksChanged;
        PlayerService.QualityChanged += OnQualityChanged;
        PlayerService.AspectRatioModeChanged += OnAspectRatioModeChanged;
        PlayerService.IsVisibleChanged += OnPlayerVisibilityChanged;
    }

    private void OnPlaybackRateChanged(double _) => RequestStateHasChanged();
    private void OnAudioTrackChanged(AudioFileTrackDto? _) => RequestStateHasChanged();
    private void OnSubtitleTrackChanged(SubtitleFileTrackDto? _) => RequestStateHasChanged();
    private void OnSubtitleTracksChanged() => RequestStateHasChanged();
    private void OnPlayerVisibilityChanged() => RequestStateHasChanged();
    private void OnQualityChanged(VideoQualityOption? _) => RequestStateHasChanged();
    private void OnAspectRatioModeChanged(AspectRatioMode _) => RequestStateHasChanged();

    private void RequestStateHasChanged()
    {
        if (_disposed)
            return;

        InvokeAsync(StateHasChanged).FireAndForget(Logger);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_open)
        {
            await ClearLayersAsync();
            return;
        }

        try
        {
            await EnsureStackLayerAsync();

            if (_activeSection != SettingsSection.None)
                await EnsureDetailLayerAsync();
            else if (_detailLayerPushed)
                await ClearDetailLayerAsync();

            if (_focusRootPending)
            {
                _focusRootPending = false;
                await SpatialNav.FocusFirstAsync(".playback-settings-panel--root .playback-settings-nav-item");
            }

            if (_focusSectionPending != SettingsSection.None && _activeSection == _focusSectionPending)
            {
                _focusSectionPending = SettingsSection.None;
                await SpatialNav.FocusFirstAsync(".playback-settings-panel--detail .playback-settings-body .k7-menu-item");
            }

            if (_activeSection == SettingsSection.None)
                return;

            await JS.InvokeVoidAsync("K7.positionPlaybackSettingsDetail", _stackRef, _detailRef);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task EnsureStackLayerAsync()
    {
        _menuCloseRef ??= DotNetObjectReference.Create(new LayerCloseCallback(() => CloseAsync().FireAndForget(Logger)));
        if (_stackLayerPushed)
        {
            await SpatialNav.AttachLayerCallbackAsync(_stackRef, _menuCloseRef);
            return;
        }

        _stackLayerPushed = true;
        await SpatialNav.PushLayerAsync(_stackRef, "popover", new SpatialNavLayerOptions
        {
            OnClose = _menuCloseRef,
            FocusSelector = ".playback-settings-panel--root .playback-settings-nav-item"
        });
    }

    private async Task EnsureDetailLayerAsync()
    {
        _detailCloseRef ??= DotNetObjectReference.Create(new LayerCloseCallback(BackToRoot));
        if (_detailLayerPushed)
        {
            await SpatialNav.AttachLayerCallbackAsync(_detailRef, _detailCloseRef);
            return;
        }

        _detailLayerPushed = true;
        await SpatialNav.PushLayerAsync(_detailRef, "popover", new SpatialNavLayerOptions
        {
            OnClose = _detailCloseRef,
            FocusSelector = ".playback-settings-body .k7-menu-item"
        });
    }

    private async Task ClearDetailLayerAsync()
    {
        if (!_detailLayerPushed)
            return;

        _detailLayerPushed = false;
        try
        {
            await SpatialNav.PopLayerAsync(_detailRef);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task ClearLayersAsync()
    {
        await ClearDetailLayerAsync();

        if (!_stackLayerPushed)
            return;

        _stackLayerPushed = false;
        try
        {
            await SpatialNav.PopLayerAsync(_stackRef);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task ToggleAsync()
    {
        _open = !_open;
        if (!_open)
        {
            _activeSection = SettingsSection.None;
            _focusSectionPending = SettingsSection.None;
            _focusRootPending = false;
        }
        else
        {
            _focusRootPending = true;
        }

        await OpenChanged.InvokeAsync(_open);
    }

    private async Task CloseAsync()
    {
        if (!_open)
            return;

        _open = false;
        _activeSection = SettingsSection.None;
        _focusSectionPending = SettingsSection.None;
        _focusRootPending = false;
        await OpenChanged.InvokeAsync(false);
    }

    private void SelectSection(SettingsSection section)
    {
        if (_activeSection == section)
        {
            _activeSection = SettingsSection.None;
            _focusSectionPending = SettingsSection.None;
            _focusRootPending = true;
            return;
        }

        _activeSection = section;
        _focusSectionPending = section;
        // Detail panel is recreated; force a fresh SpatialNav layer push + autofocus.
        _detailLayerPushed = false;
    }

    private void BackToRoot()
    {
        _activeSection = SettingsSection.None;
        _focusSectionPending = SettingsSection.None;
        _focusRootPending = true;
    }

    private int GetSectionIndex(SettingsSection section)
    {
        var sections = VisibleSections;
        for (var i = 0; i < sections.Count; i++)
        {
            if (sections[i] == section)
                return i;
        }

        return 0;
    }

    private static string GetSectionIcon(SettingsSection section) => section switch
    {
        SettingsSection.Audio => Phosphor.SpeakerHigh,
        SettingsSection.Subtitles => Phosphor.Subtitles,
        SettingsSection.Quality => Phosphor.SlidersHorizontal,
        SettingsSection.Speed => Phosphor.Gauge,
        SettingsSection.AspectRatio => Phosphor.FrameCorners,
        _ => Phosphor.Gear
    };

    private string GetSectionTitle(SettingsSection section) => section switch
    {
        SettingsSection.Audio => L["Audio"],
        SettingsSection.Subtitles => L["Subtitles"],
        SettingsSection.Quality => L["Quality"],
        SettingsSection.Speed => L["Speed"],
        SettingsSection.AspectRatio => L["AspectRatio"],
        _ => L["PlaybackSettings"]
    };

    private void OnSpeedSelected(double speed) => PlayerService.SetPlaybackRate(speed);

    private bool IsCurrentSpeed(double speed) => Math.Abs(PlayerService.PlaybackRate - speed) < 0.01;

    private void OnAspectRatioSelected(AspectRatioMode mode) => PlayerService.SetAspectRatioMode(mode);

    private string GetAspectRatioLabel(AspectRatioMode mode) => mode switch
    {
        AspectRatioMode.Fit => L["AspectRatioFit"],
        AspectRatioMode.Fill => L["AspectRatioFillCrop"],
        AspectRatioMode.Stretch => L["AspectRatioStretch"],
        _ => mode.ToString()
    };

    private async Task OnAudioTrackSelected(AudioFileTrackDto track)
    {
        if (PlayerService.SelectedAudioTrack?.Index == track.Index)
            return;

        await PlayerService.ChangeAudioTrackAsync(track);
    }

    private async Task OnSubtitleTrackSelected(SubtitleFileTrackDto? track)
    {
        if (PlayerService.SelectedSubtitleTrack?.Index == track?.Index)
            return;

        await PlayerService.ChangeSubtitleTrackAsync(track);
    }

    private async Task OnQualitySelected(VideoQualityOption quality)
    {
        if (PlayerService.SelectedQuality == quality)
            return;

        await PlayerService.ChangeQualityAsync(quality);
    }

    private static string GetAudioTrackLabel(AudioFileTrackDto track) =>
        AudioTrackDisplayHelper.FormatLabel(track);

    private string GetSubtitleTrackLabel(SubtitleFileTrackDto track)
    {
        var type = track.IsHearingImpaired
            ? L["SubtitleTypeHearingImpaired"]
            : track.IsForced
                ? L["SubtitleTypeForced"]
                : L["SubtitleTypeFull"];
        return AudioTrackDisplayHelper.FormatSubtitleLabel(track, type);
    }

    public void Dispose()
    {
        _disposed = true;
        PlayerService.PlaybackRateChanged -= OnPlaybackRateChanged;
        PlayerService.AudioTrackChanged -= OnAudioTrackChanged;
        PlayerService.SubtitleTrackChanged -= OnSubtitleTrackChanged;
        PlayerService.SubtitleTracksChanged -= OnSubtitleTracksChanged;
        PlayerService.QualityChanged -= OnQualityChanged;
        PlayerService.AspectRatioModeChanged -= OnAspectRatioModeChanged;
        PlayerService.IsVisibleChanged -= OnPlayerVisibilityChanged;
        _menuCloseRef?.Dispose();
        _detailCloseRef?.Dispose();
        _menuCloseRef = null;
        _detailCloseRef = null;
    }
}
