using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class DownloadButton : ComponentBase, IDisposable
{
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Parameter, EditorRequired]
    public Guid IndexedFileId { get; set; }

    [Parameter, EditorRequired]
    public Guid MediaId { get; set; }

    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public MediaType MediaType { get; set; } = MediaType.MusicTrack;

    [Parameter]
    public string? Artist { get; set; }

    [Parameter]
    public string? AlbumTitle { get; set; }

    [Parameter]
    public string? CoverUrl { get; set; }

    [Parameter]
    public int? AudioTrackIndex { get; set; }

    [Parameter]
    public int[]? SubtitleTrackIndices { get; set; }

    [Parameter]
    public VideoFileMetadataDto? VideoMetadata { get; set; }

    private bool _isDownloaded;
    private bool _isDownloading;

    protected override async Task OnParametersSetAsync()
    {
        _isDownloaded = await OfflineStore.IsAvailableOfflineAsync(IndexedFileId);
        _isDownloading = DownloadManager.Queue.Any(q =>
            q.Request.IndexedFileId == IndexedFileId &&
            q.Status is DownloadItemStatus.Queued or DownloadItemStatus.Preparing or DownloadItemStatus.Downloading);
    }

    protected override void OnInitialized()
    {
        DownloadManager.DownloadCompleted += OnDownloadCompleted;
        DownloadManager.ProgressChanged += OnProgressChanged;
    }

    private async Task StartDownloadAsync()
    {
        if (_isDownloaded || _isDownloading) return;

        var audioTrackIndex = AudioTrackIndex;
        var subtitleTrackIndices = SubtitleTrackIndices;

        if (VideoMetadata is not null && (VideoMetadata.AudioTracks?.Count > 1 || VideoMetadata.SubtitleTracks?.Count > 0))
        {
            var parameters = new K7DialogParameters<DownloadOptionsDialog>
            {
                { x => x.VideoMetadata, VideoMetadata }
            };

            var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
            var dialog = await DialogService.ShowAsync<DownloadOptionsDialog>(L["DownloadOptions"], parameters, options);
            var result = await dialog.Result;

            if (result is null || result.Canceled)
                return;

            if (result.Data is DownloadOptionsResult optionsResult)
            {
                audioTrackIndex = optionsResult.AudioTrack?.Index;
                subtitleTrackIndices = optionsResult.SubtitleTrack is not null ? [optionsResult.SubtitleTrack.Index] : null;
            }
        }

        await DownloadManager.EnqueueAsync(new DownloadRequest
        {
            IndexedFileId = IndexedFileId,
            MediaId = MediaId,
            Title = Title,
            Artist = Artist,
            AlbumTitle = AlbumTitle,
            CoverUrl = CoverUrl,
            MediaType = MediaType,
            AudioTrackIndex = audioTrackIndex,
            SubtitleTrackIndices = subtitleTrackIndices,
            IsCacheItem = false
        });

        _isDownloading = true;
        Snackbar.Add(string.Format(L["DownloadQueued"], Title), K7Severity.Info);
        StateHasChanged();
    }

    private async Task RemoveDownloadAsync()
    {
        await OfflineStore.RemoveAsync(IndexedFileId);
        _isDownloaded = false;
        Snackbar.Add(string.Format(L["DownloadRemoved"], Title), K7Severity.Info);
        StateHasChanged();
    }

    private void OnDownloadCompleted(DownloadCompletedInfo info)
    {
        if (info.Request.IndexedFileId == IndexedFileId)
        {
            _isDownloading = false;
            _isDownloaded = true;
            InvokeAsync(StateHasChanged);
        }
    }

    private void OnProgressChanged(DownloadProgressInfo info)
    {
        // Could be extended to show progress percentage
    }

    public void Dispose()
    {
        DownloadManager.DownloadCompleted -= OnDownloadCompleted;
        DownloadManager.ProgressChanged -= OnProgressChanged;
    }
}
