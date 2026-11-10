using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
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
    private readonly IUserRatingSync _ratings;
    private readonly ILogger<MusicRadioPlaybackService> _logger;

    private CancellationTokenSource? _refillCts;
    private MusicRadioRequest? _activeRequest;
    private bool _isRefilling;

    public MusicRadioPlaybackService(
        IServerInfoService serverInfo,
        IAudioPlayerService audio,
        IK7ServerService apiClient,
        IUserRatingSync ratings,
        ILogger<MusicRadioPlaybackService> logger)
    {
        _serverInfo = serverInfo;
        _audio = audio;
        _apiClient = apiClient;
        _ratings = ratings;
        _logger = logger;
        _audio.ActiveRadioChanged += OnActiveRadioChanged;
        _audio.RestoredPlaybackStarted += OnRestoredPlaybackStarted;
    }

    public bool IsLoading { get; private set; }
    public string? LoadingPresetTitle { get; private set; }
    public MusicRadioRequest? ActiveRequest => _activeRequest;
    public event Action? LoadingStateChanged;

    public void AttachWithoutPlaying(MusicRadioRequest request)
    {
        _refillCts?.Cancel();
        _refillCts?.Dispose();
        _refillCts = null;
        _isRefilling = false;
        _activeRequest = request;
    }

    public void BeginRefill()
    {
        if (_activeRequest is null || _refillCts is not null)
            return;

        StartRefillLoop();
    }

    public async Task<bool> StartAsync(MusicRadioRequest request, CancellationToken cancellationToken = default)
    {
        StopRefill();
        _activeRequest = request;
        SetLoading(true, request.Title);

        try
        {
            var firstBatch = await FetchTracksAsync(request, FastStartBatchSize, BuildExcludeIds(request), cancellationToken);
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

    public async Task<bool> ExtendAsync(CancellationToken cancellationToken = default)
    {
        var request = _activeRequest;
        if (request is null || _audio.ActiveRadioTitle is null)
            return false;

        // Let a refill that is already fetching finish instead of queuing the same tracks twice.
        for (var i = 0; i < 50 && _isRefilling; i++)
            await Task.Delay(100, cancellationToken);

        if (!ReferenceEquals(_activeRequest, request))
            return false;

        if (_audio.Queue.Count - _audio.CurrentIndex - 1 <= 0)
        {
            _isRefilling = true;
            try
            {
                var moreTracks = await FetchTracksAsync(request, RefillBatchSize, BuildExcludeIds(request), cancellationToken);
                if (!ReferenceEquals(_activeRequest, request))
                    return false;

                _audio.AddToQueue(moreTracks);
            }
            finally
            {
                _isRefilling = false;
            }
        }

        if (_audio.Queue.Count - _audio.CurrentIndex - 1 <= 0)
            return false;

        BeginRefill();
        return true;
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
        _audio.RestoredPlaybackStarted -= OnRestoredPlaybackStarted;
        StopRefill();
    }

    private void OnRestoredPlaybackStarted()
    {
        if (_activeRequest is not null)
            BeginRefill();
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

                    var excludeIds = BuildExcludeIds(request);
                    var moreTracks = await FetchTracksAsync(request, RefillBatchSize, excludeIds, cancellationToken);
                    _audio.AddToQueue(moreTracks);

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
            genre: request.Genre,
            limit: limit,
            excludeIds: excludeIds.Length > 0 ? excludeIds : null,
            cancellationToken: cancellationToken);

        var discovery = IsDiscoveryRadio(request.RadioType);
        return results?
            .OfType<LiteMusicTrackDto>()
            .Where(t => t.IndexedFileId.HasValue)
            .Where(t => !discovery || !IsKnownRated(t))
            .ToList() is { Count: > 0 } tracks
            ? MusicTrackQueueMapper.ToQueueItems(tracks, _apiClient)
            : [];
    }

    private Guid[] BuildExcludeIds(MusicRadioRequest request)
    {
        var ids = _audio.Queue.Select(t => t.MediaId).ToHashSet();
        // Tracks already played in this session that left the queue window.
        foreach (var played in _audio.PlayHistory)
            ids.Add(played.MediaId);
        if (IsDiscoveryRadio(request.RadioType))
        {
            // Offline / in-session ratings may not be on the server yet.
            foreach (var mediaId in _ratings.GetPositivelyRatedMediaIds())
                ids.Add(mediaId);
        }

        return ids.ToArray();
    }

    private bool IsKnownRated(LiteMusicTrackDto track)
    {
        if (track.UserRating is > 0)
            return true;
        return _ratings.TryGet(track.Id, out var cached) && cached is > 0;
    }

    private static bool IsDiscoveryRadio(string radioType) =>
        string.Equals(radioType, nameof(MusicRadioType.Discovery), StringComparison.OrdinalIgnoreCase)
        || string.Equals(radioType, nameof(MusicRadioType.DiscoveryAi), StringComparison.OrdinalIgnoreCase);

    private void SetLoading(bool isLoading, string? presetTitle)
    {
        IsLoading = isLoading;
        LoadingPresetTitle = presetTitle;
        LoadingStateChanged?.Invoke();
    }
}
