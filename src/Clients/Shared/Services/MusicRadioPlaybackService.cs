using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.Services;

public sealed class MusicRadioPlaybackService : IMusicRadioPlaybackService, IDisposable
{
    private const int FastStartBatchSize = 3;
    private const int RefillBatchSize = 25;
    private const int RefillThreshold = 8;
    private static readonly TimeSpan RefillPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan EmptyPoolRetryInterval = TimeSpan.FromSeconds(15);

    private readonly IServerInfoService _serverInfo;
    private readonly IAudioPlayerService _audio;
    private readonly IK7ServerService _apiClient;
    private readonly ILogger<MusicRadioPlaybackService> _logger;

    private CancellationTokenSource? _refillCts;
    private MusicRadioRequest? _activeRequest;
    private bool _isRefilling;

    public MusicRadioPlaybackService(
        IServerInfoService serverInfo,
        IAudioPlayerService audio,
        IK7ServerService apiClient,
        ILogger<MusicRadioPlaybackService> logger)
    {
        _serverInfo = serverInfo;
        _audio = audio;
        _apiClient = apiClient;
        _logger = logger;
        _audio.ActiveRadioChanged += OnActiveRadioChanged;
    }

    public bool IsLoading { get; private set; }
    public string? LoadingPresetTitle { get; private set; }
    public event Action? LoadingStateChanged;

    public async Task<bool> StartAsync(MusicRadioRequest request, CancellationToken cancellationToken = default)
    {
        StopRefill();
        _activeRequest = request;
        SetLoading(true, request.Title);

        try
        {
            var firstBatch = await FetchTracksAsync(request, FastStartBatchSize, [], cancellationToken);
            if (firstBatch.Count == 0)
                return false;

            await _audio.PlayRadioAsync(firstBatch, request.Title, cancellationToken: cancellationToken);
            StartRefillLoop();
            return true;
        }
        finally
        {
            SetLoading(false, null);
        }
    }

    public void StopRefill()
    {
        _refillCts?.Cancel();
        _refillCts?.Dispose();
        _refillCts = null;
        _activeRequest = null;
        _isRefilling = false;
    }

    public void Dispose()
    {
        _audio.ActiveRadioChanged -= OnActiveRadioChanged;
        StopRefill();
    }

    private void OnActiveRadioChanged()
    {
        if (_audio.ActiveRadioTitle is null)
            StopRefill();
    }

    private void StartRefillLoop()
    {
        _refillCts?.Cancel();
        _refillCts?.Dispose();
        _refillCts = new CancellationTokenSource();
        _ = RefillLoopAsync(_refillCts.Token);
    }

    private async Task RefillLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _activeRequest is not null)
        {
            try
            {
                var remaining = _audio.Queue.Count - _audio.CurrentIndex - 1;
                if (remaining >= RefillThreshold)
                {
                    await Task.Delay(RefillPollInterval, cancellationToken);
                    continue;
                }

                if (_isRefilling)
                {
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                _isRefilling = true;
                try
                {
                    var request = _activeRequest;
                    if (request is null)
                        return;

                    var excludeIds = _audio.Queue.Select(t => t.MediaId).ToArray();
                    var moreTracks = await FetchTracksAsync(request, RefillBatchSize, excludeIds, cancellationToken);
                    foreach (var track in moreTracks)
                        _audio.AddToQueue(track);

                    if (moreTracks.Count == 0)
                        await Task.Delay(EmptyPoolRetryInterval, cancellationToken);
                }
                finally
                {
                    _isRefilling = false;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Music radio refill failed");
                await Task.Delay(EmptyPoolRetryInterval, cancellationToken);
            }
        }
    }

    private async Task<List<AudioQueueItem>> FetchTracksAsync(
        MusicRadioRequest request,
        int limit,
        Guid[] excludeIds,
        CancellationToken cancellationToken)
    {
        var results = await _serverInfo.GetMusicRadioAsync(
            request.RadioType,
            request.LibraryIds,
            request.LibraryGroupIds,
            seedTrackId: request.SeedTrackId,
            seedArtistId: request.SeedArtistId,
            moodPreset: request.MoodPreset,
            moodCentroidIndex: request.MoodCentroidIndex,
            limit: limit,
            excludeIds: excludeIds.Length > 0 ? excludeIds : null,
            cancellationToken: cancellationToken);

        return results?
            .OfType<LiteMusicTrackDto>()
            .Where(t => t.IndexedFileId.HasValue)
            .ToList() is { Count: > 0 } tracks
            ? MusicTrackQueueMapper.ToQueueItems(tracks, _apiClient)
            : [];
    }

    private void SetLoading(bool isLoading, string? presetTitle)
    {
        IsLoading = isLoading;
        LoadingPresetTitle = presetTitle;
        LoadingStateChanged?.Invoke();
    }
}
