using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Clients.Shared.UI.Components.Dialogs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace K7.Clients.Shared.UI.Pages;

public partial class Movie
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public required string Id { get; set; }

    private bool isLoading { get; set; } = true;
    private static MovieDto? _movie;
    private static MediaPosterViewModel? _mediaPoster;
    private bool _isSmallDevice;
    private bool _overviewExpanded;
    private VideoFileTrackDto? _selectedVideoFileTrack;
    private AudioFileTrackDto? _selectedAudioFileTrack;

    protected override async Task OnInitializedAsync()
    {
        _movie = await k7ServerService.GetMovieAsync(Guid.Parse(Id));
        if (_movie != null)
        {
            _mediaPoster = new MediaPosterViewModel()
            {
                Id = _movie.Id.ToString(),
                Title = _movie.Title,
                PosterPictureHref = apiClient.GetAbsoluteUri(_movie.Pictures?.FirstOrDefault(x => x.Type == Server.Domain.Enums.MetadataPictureType.Poster)?.GetUri(Server.Domain.Enums.MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
            };
            _selectedVideoFileTrack = ((VideoFileMetadataDto)_movie.IndexedFiles!.First().FileMetadata!).VideoTracks.First(x => x.IsDefault);
            _selectedAudioFileTrack = ((VideoFileMetadataDto)_movie.IndexedFiles!.First().FileMetadata!).AudioTracks.First(x => x.IsDefault);
        }
        base.OnInitialized();
        isLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JSRuntime.InvokeVoidAsync("SpatialNavigation.focusFirst", "[data-nav-row] .mud-button-root");
        }
    }

    private void ScreenResized(Breakpoint breakpoint)
    {
        _isSmallDevice = breakpoint == Breakpoint.Xs;
    }

    private void ToggleOverview()
    {
        _overviewExpanded = !_overviewExpanded;
    }

    private async Task PlayAsync()
    {
        if (_movie?.IndexedFiles == null || !_movie.IndexedFiles.Any())
        {
            return;
        }

        var indexedFileId = _movie.IndexedFiles.First().Id;
        var videoMetadata = (VideoFileMetadataDto)_movie.IndexedFiles.First().FileMetadata!;
        var audioTracks = videoMetadata.AudioTracks;
        var subtitleTracks = videoMetadata.SubtitleTracks;
        var audioTrackIndex = _selectedAudioFileTrack?.Index;
        var videoResolution = videoMetadata.VideoResolution;
        var thumbnailsUrl = videoMetadata.Thumbnails?.Uri?.ToString();

        PlaybackProgressTracker.StartTracking(_movie.Id, await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress));

        await PlayerService.PlayIndexedFileAsync(indexedFileId, audioTracks, subtitleTracks, audioTrackIndex, videoResolution, thumbnailsUrl);

        if (await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback)
            && _movie.UserState is { LastPlaybackPosition: > 0, IsCompleted: false })
        {
            PlayerService.Seek(_movie.UserState.LastPlaybackPosition);
        }
    }

private async Task OpenMediaReIdentifyDialogAsync()
    {
        if (_movie == null) return;

        var parameters = new DialogParameters<ReIdentifyDialog>
        {
            { x => x.MediaId, _movie.Id },
            { x => x.InitialSearchQuery, _movie.Title }
        };

        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>("Re-identify media", parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            Snackbar.Add("Ré-identification du média envoyée et en cours de traitement par le serveur en tâche de fond.", Severity.Success);
            NavigationManager.NavigateTo("/");
        }
    }

    private async Task OpenFileReIdentifyDialogAsync(Guid indexedFileId)
    {
        var parameters = new DialogParameters<ReIdentifyDialog>
        {
            { x => x.IndexedFileId, indexedFileId },
            { x => x.InitialSearchQuery, _movie?.Title }
        };

        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>("Re-identify file", parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            Snackbar.Add("Ré-identification du fichier envoyée et en cours de traitement par le serveur en tâche de fond.", Severity.Success);
            NavigationManager.NavigateTo("/");
        }
    }
}
