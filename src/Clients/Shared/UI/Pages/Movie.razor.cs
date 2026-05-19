using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Clients.Shared.UI.Components.Dialogs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages;

public partial class Movie
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    [Parameter] public required string Id { get; set; }

    private bool isLoading { get; set; } = true;
    private static MovieDto? _movie;
    private static MediaCardViewModel? _mediaCard;
    private bool _overviewExpanded;
    private IndexedFileDto? _selectedFile;
    private AudioFileTrackDto? _selectedAudioFileTrack;
    private SubtitleFileTrackDto? _selectedSubtitleFileTrack;
    private List<MediaCardViewModel> _similarMedia = [];
    private string? _previousId;

    protected override async Task OnParametersSetAsync()
    {
        if (_previousId == Id) return;
        _previousId = Id;

        isLoading = true;
        _similarMedia = [];

        _movie = await k7ServerService.GetMovieAsync(Guid.Parse(Id));
        if (_movie != null)
        {
            _mediaCard = new MediaCardViewModel()
            {
                Id = _movie.Id.ToString(),
                Title = _movie.Title,
                PictureUrl = apiClient.GetAbsoluteUri(_movie.Pictures?.FirstOrDefault(x => x.Type == Server.Domain.Enums.MetadataPictureType.Poster)?.GetUri(Server.Domain.Enums.MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
            };
            
            _selectedFile = _movie.IndexedFiles?.FirstOrDefault();
            if (_selectedFile?.FileMetadata is VideoFileMetadataDto vMeta)
            {
                _selectedAudioFileTrack = vMeta.AudioTracks?.FirstOrDefault(x => x.IsDefault) ?? vMeta.AudioTracks?.FirstOrDefault();
                _selectedSubtitleFileTrack = vMeta.SubtitleTracks?.FirstOrDefault(x => x.IsDefault);
            }
        }
        isLoading = false;

        _ = LoadSimilarMediaAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await SpatialNav.FocusFirstAsync(".k7-btn--filled");
            }
            catch (InvalidOperationException) { }
        }
    }

    private void ToggleOverview()
    {
        _overviewExpanded = !_overviewExpanded;
    }

    private async Task PlayAsync()
    {
        if (_movie?.IndexedFiles == null || !_movie.IndexedFiles.Any() || _selectedFile == null)
        {
            return;
        }

        var indexedFileId = _selectedFile.Id;
        if (_selectedFile.FileMetadata is not VideoFileMetadataDto videoMetadata)
        {
            return;
        }
        
        var audioTracks = videoMetadata.AudioTracks;
        var subtitleTracks = videoMetadata.SubtitleTracks;
        var audioTrackIndex = _selectedAudioFileTrack?.Index;
        var subtitleTrackIndex = _selectedSubtitleFileTrack?.Index;
        var videoResolution = videoMetadata.VideoResolution;
        var thumbnailsUrl = videoMetadata.Thumbnails?.Uri?.ToString();

        PlaybackProgressTracker.StartTracking(_movie.Id, await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress), indexedFileId: indexedFileId);

        await PlayerService.PlayIndexedFileAsync(indexedFileId, audioTracks ?? [], subtitleTracks, audioTrackIndex, subtitleTrackIndex, videoResolution, thumbnailsUrl, _movie.Id);

        if (await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback)
            && _movie.UserState is { LastPlaybackPosition: > 0, IsCompleted: false })
        {
            PlayerService.Seek(_movie.UserState.LastPlaybackPosition);
        }
    }

    private async Task OpenPlaybackOptionsAsync()
    {
        if (_movie?.IndexedFiles == null || !_movie.IndexedFiles.Any()) return;

        var parameters = new K7DialogParameters<PlaybackOptionsDialog>
        {
            { x => x.Movie, _movie },
            { x => x.InitialFileId, _selectedFile?.Id }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        
        // I use localizer from the dialog to avoid injecting it here just for the title
        var title = _movie.IndexedFiles.Count > 1 ? L["IndexedVersions"] : L["AudioTrack"]; // Fallbacks until we can rely on PlaybackOptionsDialog.resx properly.
        var dialog = await DialogService.ShowAsync<PlaybackOptionsDialog>(title, parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled && result.Data is PlaybackOptionsResult optionsResult)
        {
            _selectedFile = optionsResult.SelectedFile;
            _selectedAudioFileTrack = optionsResult.AudioTrack;
            _selectedSubtitleFileTrack = optionsResult.SubtitleTrack;
            
            await PlayAsync();
        }
    }

    private async Task OpenMediaReIdentifyDialogAsync()
    {
        if (_movie == null) return;

        var parameters = new K7DialogParameters<ReIdentifyDialog>
        {
            { x => x.MediaId, _movie.Id },
            { x => x.InitialSearchQuery, _movie.Title }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(L["ReIdentifyMediaDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            Snackbar.Add(L["ReIdentifyMediaSent"], K7Severity.Success);
            NavigationManager.NavigateTo("/");
        }
    }

    private async Task OpenFileReIdentifyDialogAsync(Guid indexedFileId)
    {
        var parameters = new K7DialogParameters<ReIdentifyDialog>
        {
            { x => x.IndexedFileId, indexedFileId },
            { x => x.InitialSearchQuery, _movie?.Title }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(L["ReIdentifyFileDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            Snackbar.Add(L["ReIdentifyFileSent"], K7Severity.Success);
            NavigationManager.NavigateTo("/");
        }
    }

    private async Task OpenIndexedFilesDialogAsync()
    {
        if (_movie == null) return;

        var parameters = new K7DialogParameters<IndexedFilesDialog>
        {
            { x => x.Media, _movie },
            { x => x.OnReIdentifyFile, EventCallback.Factory.Create<Guid>(this, OpenFileReIdentifyDialogAsync) }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        await DialogService.ShowAsync<IndexedFilesDialog>(L["IndexedVersions"], parameters, options);
    }

    private Task OpenSynopsisDialogAsync()
    {
        if (_movie == null || string.IsNullOrWhiteSpace(_movie.Overview)) return Task.CompletedTask;

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var parameters = new K7DialogParameters
        {
            { "ContentText", _movie.Overview },
            { "ButtonText", S["Cancel"].Value }
        };
        return DialogService.ShowAsync<OverviewDialog>(L["Overview"], parameters, options);
    }

    private async Task RefreshMetadataAsync()
    {
        if (_movie is null) return;

        try
        {
            await k7ServerService.RefreshMediaMetadataAsync(_movie.Id);
            Snackbar.Add(L["RefreshMetadataSent"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private Task OpenTrailerAsync()
    {
        if (_movie?.Trailers is not { Count: > 0 }) return Task.CompletedTask;

        var trailer = _movie.Trailers.FirstOrDefault(t => t.Type == "Trailer") ?? _movie.Trailers[0];
        var parameters = new K7DialogParameters<TrailerDialog>
        {
            { x => x.TrailerKey, trailer.Key },
            { x => x.TrailerSite, trailer.Site ?? "YouTube" }
        };
        var options = new K7DialogOptions { FullScreen = true, CloseOnEscapeKey = true, CloseButton = true };
        return DialogService.ShowAsync<TrailerDialog>(trailer.Name ?? L["Trailer"], parameters, options);
    }

    private void NavigateToStudio(string studio)
    {
        NavigationManager.NavigateTo($"/search?studio={Uri.EscapeDataString(studio)}");
    }

    private async Task LoadSimilarMediaAsync()
    {
        if (_movie is null) return;

        try
        {
            var similar = await k7ServerService.GetSimilarMediaAsync(_movie.Id);
            _similarMedia = similar.Select(m => new MediaCardViewModel
            {
                Id = m.Id.ToString(),
                Title = m.Title,
                AdditionalInformations = m.ReleaseDate?.Year.ToString(),
                PictureUrl = apiClient.GetAbsoluteUri(
                    m.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
                        ?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
            }).ToList();
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // Non-critical - silently ignore if similar media fails
        }
    }
}
