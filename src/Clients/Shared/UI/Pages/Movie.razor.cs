using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Clients.Shared.UI.Components.Dialogs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    private static MediaCardViewModel? _mediaCard;
    private bool _isSmallDevice;
    private bool _overviewExpanded;
    private IndexedFileDto? _selectedFile;
    private AudioFileTrackDto? _selectedAudioFileTrack;
    private SubtitleFileTrackDto? _selectedSubtitleFileTrack;

    protected override async Task OnInitializedAsync()
    {
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

    private async Task HandleOverviewKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " ")
        {
            await OpenSynopsisDialogAsync();
        }
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

        PlaybackProgressTracker.StartTracking(_movie.Id, await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress));

        await PlayerService.PlayIndexedFileAsync(indexedFileId, audioTracks ?? [], subtitleTracks, audioTrackIndex, subtitleTrackIndex, videoResolution, thumbnailsUrl);

        if (await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback)
            && _movie.UserState is { LastPlaybackPosition: > 0, IsCompleted: false })
        {
            PlayerService.Seek(_movie.UserState.LastPlaybackPosition);
        }
    }

    private async Task OpenPlaybackOptionsAsync()
    {
        if (_movie?.IndexedFiles == null || !_movie.IndexedFiles.Any()) return;

        var parameters = new DialogParameters<PlaybackOptionsDialog>
        {
            { x => x.Movie, _movie },
            { x => x.InitialFileId, _selectedFile?.Id }
        };

        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        
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

        var parameters = new DialogParameters<ReIdentifyDialog>
        {
            { x => x.MediaId, _movie.Id },
            { x => x.InitialSearchQuery, _movie.Title }
        };

        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(L["ReIdentifyMediaDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            Snackbar.Add(L["ReIdentifyMediaSent"], Severity.Success);
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
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(L["ReIdentifyFileDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            Snackbar.Add(L["ReIdentifyFileSent"], Severity.Success);
            NavigationManager.NavigateTo("/");
        }
    }

    private async Task OpenIndexedFilesDialogAsync()
    {
        if (_movie == null) return;

        var parameters = new DialogParameters<IndexedFilesDialog>
        {
            { x => x.Media, _movie },
            { x => x.OnReIdentifyFile, EventCallback.Factory.Create<Guid>(this, OpenFileReIdentifyDialogAsync) }
        };

        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        await DialogService.ShowAsync<IndexedFilesDialog>(L["IndexedVersions"], parameters, options);
    }

    private Task OpenSynopsisDialogAsync()
    {
        if (_movie == null || string.IsNullOrWhiteSpace(_movie.Overview)) return Task.CompletedTask;

        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var parameters = new DialogParameters
        {
            { "ContentText", _movie.Overview },
            { "ButtonText", S["Cancel"].Value }
        };
        return DialogService.ShowAsync<OverviewDialog>(L["Overview"], parameters, options);
    }
}
