using K7.Clients.Shared.Domain.Models;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.Pages;

public partial class Movie
{
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
                PosterPictureHref = k7ServerService.GetAbsoluteUri(_movie.Pictures?.FirstOrDefault(x => x.Type == Server.Domain.Enums.MetadataPictureType.Poster)?.Uri?.OriginalString)?.AbsoluteUri
            };
            _selectedVideoFileTrack = ((VideoFileMetadataDto)_movie.IndexedFiles!.First().FileMetadata!).VideoTracks.First(x => x.IsDefault);
            _selectedAudioFileTrack = ((VideoFileMetadataDto)_movie.IndexedFiles!.First().FileMetadata!).AudioTracks.First(x => x.IsDefault);
        }
        base.OnInitialized();
        isLoading = false;
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
        await PlayerService.PlayIndexedFileAsync(indexedFileId);
    }
}
