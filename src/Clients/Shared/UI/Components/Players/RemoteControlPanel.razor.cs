using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Interfaces;
using K7.Shared.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class RemoteControlPanel : ComponentBase, IAsyncDisposable
{
    [Parameter] public EventCallback OnResumeRequested { get; set; }

    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private ElementReference _root;
    private DotNetObjectReference<LayerCloseCallback>? _backRef;
    private Uri? _thumbnailsUri;
    private List<SeekBar.Chapter> _chapters = [];
    private bool _spatialNavReady;
    private bool _disposed;
    private bool _isMenuOpen;

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
            _backRef ??= DotNetObjectReference.Create(new LayerCloseCallback(() => _ = OnLeaveWithoutStop()));
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
        _thumbnailsUri = ResolveThumbnailsUri(Remote.ThumbnailsUrl);
        if (_thumbnailsUri is null)
            _ = LoadThumbnailsFromMediaAsync();

        var fileChapters = Remote.Chapters.Count > 0 ? Remote.Chapters : source?.Chapters;
        var markers = SeekBarChapterBuilder.Build(
            showChapterTicks: true,
            fileChapters,
            segments: null,
            introTitle: S["Intro"],
            outroTitle: S["Outro"]);

        _chapters = markers
            .Select(m => new SeekBar.Chapter { Title = m.Title, Start = m.StartSeconds })
            .ToList();
    }

    private Uri? ResolveThumbnailsUri(string? relativeOrAbsolute)
    {
        var display = MediaPictureUrlHelper.ToDisplayUrl(K7Server, relativeOrAbsolute);
        if (string.IsNullOrEmpty(display))
            return null;

        if (Uri.TryCreate(display, UriKind.Absolute, out var absolute))
            return absolute;

        return K7Server.GetAbsoluteUri(display);
    }

    private async Task LoadThumbnailsFromMediaAsync()
    {
        if (_disposed || Remote.MediaId is not Guid mediaId)
            return;

        MediaDto? media;
        try
        {
            media = await MediaService.GetMediaAsync(mediaId);
        }
        catch
        {
            return;
        }

        if (_disposed || _thumbnailsUri is not null)
            return;

        var indexedFile = media?.IndexedFiles?.FirstOrDefault(f => f.Id == Remote.IndexedFileId)
            ?? media?.IndexedFiles?.FirstOrDefault();
        var thumbs = (indexedFile?.FileMetadata as VideoFileMetadataDto)?.Thumbnails?.Uri?.OriginalString;
        var resolved = ResolveThumbnailsUri(thumbs);
        if (resolved is null)
            return;

        _thumbnailsUri = resolved;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnPlayPause()
    {
        if (Remote.PlaybackState == RemotePlaybackState.Playing)
            await Remote.SendPauseAsync();
        else
            await Remote.SendPlayAsync();
    }

    private async Task OnStop()
    {
        if (_disposed || !Remote.IsControlling)
            return;

        await Remote.SendStopAsync();
        PlayerService.Stop();
        await PlayerService.HideAsync();
    }

    private async Task OnLeaveWithoutStop()
    {
        if (_disposed || !Remote.IsControlling)
            return;

        await Remote.ReleaseControlAsync();
        PlayerService.Stop();
        await PlayerService.HideAsync();
    }

    private async Task OnSeekAsync(double position) => await Remote.SendSeekAsync(position);

    private async Task OnVolumeChanged(double volume) => await Remote.SendVolumeAsync(volume);

    private async Task OnResumeHere() => await OnResumeRequested.InvokeAsync();

    private async Task OnGoToMedia()
    {
        if (Remote.MediaId is not Guid mediaId)
            return;

        MediaDto? media;
        try
        {
            media = await MediaService.GetMediaAsync(mediaId);
        }
        catch
        {
            return;
        }

        var href = BuildMediaHref(media);
        if (string.IsNullOrEmpty(href))
            return;

        await OnLeaveWithoutStop();
        Navigation.NavigateTo(href);
    }

    private static string? BuildMediaHref(MediaDto? media) => media switch
    {
        MovieDto movie => MediaPageUrls.Build(MediaType.Movie, movie.Id),
        SerieDto serie => MediaPageUrls.Build(MediaType.Serie, serie.Id),
        SerieSeasonDto season => MediaPageUrls.Build(MediaType.SerieSeason, season.Id, season.SerieId, season.SeasonNumber),
        SerieEpisodeDto episode => MediaPageUrls.Build(
            MediaType.SerieEpisode,
            episode.Id,
            episode.SerieId,
            episode.SeasonNumber,
            episode.EpisodeNumber),
        MusicAlbumDto album => MediaPageUrls.Build(MediaType.MusicAlbum, album.Id),
        MusicTrackDto track => MediaPageUrls.Build(MediaType.MusicTrack, track.Id, albumId: track.AlbumId),
        MusicArtistDto artist => MediaPageUrls.Build(MediaType.MusicArtist, artist.Id),
        _ => null
    };

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
