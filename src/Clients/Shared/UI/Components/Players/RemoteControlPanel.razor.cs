using System.Globalization;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class RemoteControlPanel : ComponentBase, IAsyncDisposable
{
    [Parameter] public EventCallback OnResumeRequested { get; set; }

    private ElementReference _root;
    private DotNetObjectReference<LayerCloseCallback>? _backRef;
    private Uri? _thumbnailsUri;
    private List<SeekBar.Chapter> _chapters = [];
    private bool _spatialNavReady;
    private bool _disposed;

    protected override void OnInitialized()
    {
        Remote.StateChanged += OnStateChanged;
        Remote.SessionChanged += OnSessionChanged;
        RefreshSeekMetadata();
    }

    protected override void OnParametersSet() => RefreshSeekMetadata();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || _spatialNavReady)
            return;

        try
        {
            _backRef ??= DotNetObjectReference.Create(new LayerCloseCallback(() => _ = ExitRemoteAsync()));
            await JSRuntime.InvokeVoidAsync("SpatialNav.registerVideoPlayerBack", _backRef);
            await SpatialNav.AttachLayerCallbackAsync(_root, _backRef);
            await SpatialNav.FocusFirstAsync(".remote-control-panel__play, .remote-control-panel .focusable");
            _spatialNavReady = true;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
        {
        }
    }

    private void OnStateChanged()
    {
        // Position ticks every second - only re-render when chrome that owns menus / transport changes.
        // Full redraws were closing K7Menu (Open default false re-applied on ParametersSet).
        if (!ShouldRefreshChrome())
            return;

        CaptureChromeSnapshot();
        InvokeAsync(StateHasChanged);
    }

    private void OnSessionChanged()
    {
        RefreshSeekMetadata();
        CaptureChromeSnapshot();
        InvokeAsync(StateHasChanged);
    }

    private RemotePlaybackState _chromeState;
    private double _chromeVolume = -1;
    private int? _chromeAudioIndex;
    private int? _chromeSubtitleIndex;
    private int _chromeAudioCount = -1;
    private int _chromeSubtitleCount = -1;

    private bool ShouldRefreshChrome() =>
        Remote.PlaybackState != _chromeState
        || Math.Abs(Remote.Volume - _chromeVolume) > 0.02
        || Remote.SelectedAudioTrackIndex != _chromeAudioIndex
        || Remote.SelectedSubtitleTrackIndex != _chromeSubtitleIndex
        || Remote.AudioTracks.Count != _chromeAudioCount
        || Remote.SubtitleTracks.Count != _chromeSubtitleCount;

    private void CaptureChromeSnapshot()
    {
        _chromeState = Remote.PlaybackState;
        _chromeVolume = Remote.Volume;
        _chromeAudioIndex = Remote.SelectedAudioTrackIndex;
        _chromeSubtitleIndex = Remote.SelectedSubtitleTrackIndex;
        _chromeAudioCount = Remote.AudioTracks.Count;
        _chromeSubtitleCount = Remote.SubtitleTracks.Count;
    }

    private void RefreshSeekMetadata()
    {
        var source = PlayerService.Source;
        _thumbnailsUri = ResolveThumbnailsUri(source?.ThumbnailsUrl);

        var markers = SeekBarChapterBuilder.Build(
            showChapterTicks: true,
            source?.Chapters,
            segments: null,
            introTitle: S["Intro"],
            outroTitle: S["Outro"]);

        _chapters = markers
            .Select(m => new SeekBar.Chapter { Title = m.Title, Start = m.StartSeconds })
            .ToList();
    }

    private Uri? ResolveThumbnailsUri(string? relativeOrAbsolute)
    {
        if (string.IsNullOrEmpty(relativeOrAbsolute))
            return null;

        if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var absolute))
            return absolute;

        var baseAddress = K7Server.HttpClient.BaseAddress;
        if (baseAddress is null)
            return Uri.TryCreate(relativeOrAbsolute, UriKind.RelativeOrAbsolute, out var relative) ? relative : null;

        return new Uri(baseAddress, relativeOrAbsolute);
    }

    private async Task OnPlayPause()
    {
        if (Remote.PlaybackState == RemotePlaybackState.Playing)
            await Remote.SendPauseAsync();
        else
            await Remote.SendPlayAsync();
    }

    private async Task OnStop() => await ExitRemoteAsync();

    private async Task OnSeekAsync(double position) => await Remote.SendSeekAsync(position);

    private async Task OnVolumeInput(ChangeEventArgs e)
    {
        if (double.TryParse(
                e.Value?.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var volume))
        {
            await Remote.SendVolumeAsync(volume);
        }
    }

    private async Task OnAudioTrackSelected(int trackIndex) =>
        await Remote.SendAudioTrackAsync(trackIndex);

    private async Task OnSubtitleTrackSelected(int trackIndex) =>
        await Remote.SendSubtitleTrackAsync(trackIndex);

    private async Task OnResumeHere() => await OnResumeRequested.InvokeAsync();

    private async Task ExitRemoteAsync()
    {
        if (_disposed || !Remote.IsControlling)
            return;

        await Remote.SendStopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        Remote.StateChanged -= OnStateChanged;
        Remote.SessionChanged -= OnSessionChanged;

        try
        {
            await JSRuntime.InvokeVoidAsync("SpatialNav.unregisterVideoPlayerBack");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
        {
        }

        _backRef?.Dispose();
        _backRef = null;
    }
}
