using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;

namespace K7.Server.Application.Services;

/// <summary>
/// Downloads origin direct-stream bytes into a local sparse-friendly file (4 MiB pieces).
/// Prefetch follows the playback head forward so seeks do not backfill unused ranges.
/// </summary>
public sealed class FederatedMediaCache(
    IServiceScopeFactory scopeFactory,
    IOptions<PathsConfiguration> pathsOptions,
    ILogger<FederatedMediaCache> logger) : IFederatedMediaCache
{
    private const int MaxParallelPieceFetches = 6;

    private readonly ConcurrentDictionary<Guid, CacheSlot> _slots = new();
    private readonly PathsConfiguration _paths = pathsOptions.Value;
    private readonly SemaphoreSlim _openLock = new(1, 1);

    public async Task<FederatedMediaCacheEntry> EnsureOpenedAsync(
        Guid remoteIndexedFileId,
        Guid peerServerId,
        Guid remoteSessionId,
        string originDirectStreamUrl,
        CancellationToken cancellationToken = default)
    {
        if (_slots.TryGetValue(remoteIndexedFileId, out var existing)
            && File.Exists(existing.Entry.LocalPath)
            && existing.Entry.ContentLength > 0)
        {
            RefreshEntry(existing.Entry, peerServerId, remoteSessionId, originDirectStreamUrl);
            return existing.Entry;
        }

        await _openLock.WaitAsync(cancellationToken);
        try
        {
            if (_slots.TryGetValue(remoteIndexedFileId, out existing)
                && File.Exists(existing.Entry.LocalPath)
                && existing.Entry.ContentLength > 0)
            {
                RefreshEntry(existing.Entry, peerServerId, remoteSessionId, originDirectStreamUrl);
                return existing.Entry;
            }

            var transcodingRoot = _paths.Transcoding
                ?? throw new InvalidOperationException("Paths:Transcoding is not configured.");
            var dir = Path.Combine(transcodingRoot, "federation-media", remoteIndexedFileId.ToString("N"));
            Directory.CreateDirectory(dir);
            var localPath = Path.Combine(dir, "media.bin");
            var mapPath = Path.Combine(dir, "pieces.map");

            var contentLength = await ResolveContentLengthAsync(
                peerServerId, remoteSessionId, cancellationToken);
            if (contentLength <= 0)
                throw new InvalidOperationException(
                    $"Federated media Content-Length unavailable for {remoteIndexedFileId:N}.");

            await EnsureSparseFileAsync(localPath, contentLength, cancellationToken);

            var map = new FederatedMediaPieceMap(contentLength);
            LoadMap(map, mapPath);

            var entry = new FederatedMediaCacheEntry
            {
                RemoteIndexedFileId = remoteIndexedFileId,
                PeerServerId = peerServerId,
                RemoteSessionId = remoteSessionId,
                OriginDirectStreamUrl = originDirectStreamUrl,
                LocalPath = localPath,
                ContentLength = contentLength,
                LastAccessUtc = DateTime.UtcNow
            };

            _slots[remoteIndexedFileId] = new CacheSlot(entry, map, mapPath);
            logger.LogInformation(
                "Opened federated media cache for {RemoteIndexedFileId} at {LocalPath} ({ContentLength} bytes)",
                remoteIndexedFileId,
                localPath,
                contentLength);
            return entry;
        }
        finally
        {
            _openLock.Release();
        }
    }

    public async Task EnsureHeaderAndCuesAsync(
        FederatedMediaCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        // Head and cues tail do not overlap on a normal file. Fetch them together.
        // Do not prefetch from byte 0 here: a resume would spend the parallel slots
        // on the title start while ffmpeg waits for the seek window.
        var (headOffset, headCount) = FederatedMediaPieceMap.HeaderAndCuesRange(entry.ContentLength);
        var tail = FederatedMediaPieceMap.TailRange(entry.ContentLength);
        var fetches = new List<Task>(2);
        if (headCount > 0)
        {
            fetches.Add(EnsureRangeAsync(
                entry, headOffset, headCount, updatePlaybackHead: false, cancellationToken));
        }

        if (tail is { } t)
        {
            fetches.Add(EnsureRangeAsync(
                entry, t.Offset, t.Count, updatePlaybackHead: false, cancellationToken));
        }

        if (fetches.Count > 0)
            await Task.WhenAll(fetches);
    }

    public Task EnsureRangeAsync(
        FederatedMediaCacheEntry entry,
        long offset,
        long count,
        CancellationToken cancellationToken = default) =>
        EnsureRangeAsync(entry, offset, count, updatePlaybackHead: true, cancellationToken);

    public async Task EnsureTimeWindowAsync(
        FederatedMediaCacheEntry entry,
        TimeSpan windowStart,
        TimeSpan windowEnd,
        TimeSpan? mediaDuration,
        CancellationToken cancellationToken = default,
        long runwayBytes = 0)
    {
        var runway = runwayBytes > 0
            ? runwayBytes
            : FederatedMediaPieceMap.EnsureRunwayBytes;
        var (offset, count) = FederatedMediaPieceMap.ApproximateTimeWindow(
            entry.ContentLength, windowStart, windowEnd, mediaDuration, runway);
        await EnsureRangeAsync(entry, offset, count, updatePlaybackHead: true, cancellationToken);
    }

    public bool TryGet(Guid remoteIndexedFileId, out FederatedMediaCacheEntry? entry)
    {
        if (_slots.TryGetValue(remoteIndexedFileId, out var slot))
        {
            entry = slot.Entry;
            return true;
        }

        entry = null;
        return false;
    }

    private async Task EnsureRangeAsync(
        FederatedMediaCacheEntry entry,
        long offset,
        long count,
        bool updatePlaybackHead,
        CancellationToken cancellationToken)
    {
        if (!_slots.TryGetValue(entry.RemoteIndexedFileId, out var slot))
            throw new InvalidOperationException(
                $"Federated media cache not open for {entry.RemoteIndexedFileId:N}.");

        RefreshEntry(entry, entry.PeerServerId, entry.RemoteSessionId, entry.OriginDirectStreamUrl);
        slot.Entry.LastAccessUtc = DateTime.UtcNow;
        if (updatePlaybackHead)
            SetPlaybackHead(entry.RemoteIndexedFileId, offset);

        List<(long Offset, long Count)> chunks;
        lock (slot.MapSync)
        {
            chunks = slot.Map.MissingRanges(offset, count)
                .SelectMany(r => FederatedMediaPieceMap.ChunkRange(r.Offset, r.Count, entry.ContentLength))
                .ToList();
        }

        if (chunks.Count > 0)
        {
            await Parallel.ForEachAsync(
                chunks,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxParallelPieceFetches,
                    CancellationToken = cancellationToken
                },
                async (chunk, ct) => await FetchAndWritePieceAsync(slot, chunk.Offset, chunk.Count, ct));

            lock (slot.MapSync)
                PersistMap(slot);
        }

        StartForwardPrefetch(entry);
    }

    private void SetPlaybackHead(Guid remoteIndexedFileId, long offset)
    {
        if (_slots.TryGetValue(remoteIndexedFileId, out var slot))
            Volatile.Write(ref slot.PlaybackHeadOffset, Math.Max(0, offset));
    }

    private void StartForwardPrefetch(FederatedMediaCacheEntry entry)
    {
        if (!_slots.TryGetValue(entry.RemoteIndexedFileId, out var slot))
            return;

        if (Interlocked.CompareExchange(ref slot.PrefetchRunning, 1, 0) != 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await ForwardPrefetchAsync(entry);
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    ex,
                    "Federated media forward prefetch failed for {RemoteIndexedFileId}",
                    entry.RemoteIndexedFileId);
            }
            finally
            {
                Interlocked.Exchange(ref slot.PrefetchRunning, 0);
            }
        }, CancellationToken.None);
    }

    private async Task ForwardPrefetchAsync(FederatedMediaCacheEntry entry)
    {
        if (!_slots.TryGetValue(entry.RemoteIndexedFileId, out var slot))
            return;

        // Only fill ahead of the playback head. Never backfill 0..head (seek/resume).
        var idleRounds = 0;
        while (idleRounds < 50)
        {
            var head = Volatile.Read(ref slot.PlaybackHeadOffset);
            var remaining = Math.Max(0, entry.ContentLength - head);
            if (remaining <= 0)
                return;

            var window = Math.Min(FederatedMediaPieceMap.PrefetchWindowBytes, remaining);
            List<(long Offset, long Count)> chunks;
            lock (slot.MapSync)
            {
                chunks = slot.Map.MissingRanges(head, window)
                    .SelectMany(r => FederatedMediaPieceMap.ChunkRange(r.Offset, r.Count, entry.ContentLength))
                    .Take(MaxParallelPieceFetches * 2)
                    .ToList();
            }

            if (chunks.Count == 0)
            {
                idleRounds++;
                await Task.Delay(200);
                continue;
            }

            idleRounds = 0;
            await Parallel.ForEachAsync(
                chunks,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelPieceFetches },
                async (chunk, ct) => await FetchAndWritePieceAsync(slot, chunk.Offset, chunk.Count, ct));

            lock (slot.MapSync)
                PersistMap(slot);

            await Task.Yield();
        }
    }

    private async Task FetchAndWritePieceAsync(
        CacheSlot slot,
        long offset,
        long count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
            return;

        lock (slot.MapSync)
        {
            if (slot.Map.MissingRanges(offset, count).Count == 0)
                return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var peerAuthorization = scope.ServiceProvider.GetRequiredService<IPeerAuthorizationService>();
        var peerClient = scope.ServiceProvider.GetRequiredService<IPeerClient>();

        var auth = await peerAuthorization.AuthenticateOutboundAsync(
            slot.Entry.PeerServerId, cancellationToken);
        if (auth is null)
            throw new InvalidOperationException(
                $"Failed to authenticate with peer {slot.Entry.PeerServerId} for media cache.");

        var (peer, token) = auth.Value;
        var endInclusive = offset + count - 1;
        var rangeHeader = $"bytes={offset.ToString(CultureInfo.InvariantCulture)}-{endInclusive.ToString(CultureInfo.InvariantCulture)}";

        using var response = await peerClient.ProxyStreamContentAsync(
            peer.BaseUrl,
            token,
            slot.Entry.RemoteSessionId,
            "direct-stream",
            cancellationToken,
            rangeHeader);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Federated media Range fetch failed with {(int)response.StatusCode} for {slot.Entry.RemoteIndexedFileId:N}.");
        }

        if (response.StatusCode == HttpStatusCode.OK && offset > 0)
        {
            throw new HttpRequestException(
                $"Origin ignored Range for federated media {slot.Entry.RemoteIndexedFileId:N} (HTTP 200 at offset {offset}).");
        }

        await using var network = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file = new FileStream(
            slot.Entry.LocalPath,
            FileMode.Open,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 80 * 1024,
            FileOptions.RandomAccess | FileOptions.Asynchronous);

        file.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[80 * 1024];
        long written = 0;
        while (written < count)
        {
            var toRead = (int)Math.Min(buffer.Length, count - written);
            var read = await network.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
            if (read == 0)
                break;

            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            written += read;
        }

        if (written != count)
        {
            throw new IOException(
                $"Federated media Range incomplete for {slot.Entry.RemoteIndexedFileId:N}: wrote {written} of {count} bytes at {offset}.");
        }

        await file.FlushAsync(cancellationToken);
        lock (slot.MapSync)
            slot.Map.MarkRangePresent(offset, written);

        logger.LogDebug(
            "Cached federated media piece {Offset}-{End} for {RemoteIndexedFileId} ({Written} bytes)",
            offset,
            offset + written - 1,
            slot.Entry.RemoteIndexedFileId,
            written);
    }

    private async Task<long> ResolveContentLengthAsync(
        Guid peerServerId,
        Guid remoteSessionId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var peerAuthorization = scope.ServiceProvider.GetRequiredService<IPeerAuthorizationService>();
        var peerClient = scope.ServiceProvider.GetRequiredService<IPeerClient>();

        var auth = await peerAuthorization.AuthenticateOutboundAsync(peerServerId, cancellationToken);
        if (auth is null)
            return 0;

        var (peer, token) = auth.Value;
        using var response = await peerClient.ProxyStreamContentAsync(
            peer.BaseUrl,
            token,
            remoteSessionId,
            "direct-stream",
            cancellationToken,
            "bytes=0-0");

        if (!response.IsSuccessStatusCode)
            return 0;

        if (response.Content.Headers.ContentRange?.Length is long len && len > 0)
            return len;

        if (response.Content.Headers.ContentLength is long cl && cl > 0)
            return cl;

        if (response.Headers.TryGetValues("Content-Range", out var values))
        {
            var raw = values.FirstOrDefault();
            if (raw is not null)
            {
                var slash = raw.LastIndexOf('/');
                if (slash >= 0
                    && long.TryParse(
                        raw[(slash + 1)..],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsed)
                    && parsed > 0)
                {
                    return parsed;
                }
            }
        }

        return 0;
    }

    private static async Task EnsureSparseFileAsync(
        string localPath,
        long contentLength,
        CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(
            localPath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 1,
            FileOptions.Asynchronous);

        TryEnableSparse(fs.SafeFileHandle);

        if (fs.Length != contentLength)
            fs.SetLength(contentLength);

        await fs.FlushAsync(cancellationToken);
    }

    private static void TryEnableSparse(SafeFileHandle handle)
    {
        if (!OperatingSystem.IsWindows() || handle.IsInvalid)
            return;

        const int fsctlSetSparse = 0x000900c4;
        _ = DeviceIoControl(
            handle,
            fsctlSetSparse,
            inBuffer: nint.Zero,
            nInBufferSize: 0,
            outBuffer: nint.Zero,
            nOutBufferSize: 0,
            out _,
            overlapped: nint.Zero);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        int dwIoControlCode,
        nint inBuffer,
        int nInBufferSize,
        nint outBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        nint overlapped);

    private static void LoadMap(FederatedMediaPieceMap map, string mapPath)
    {
        if (!File.Exists(mapPath))
            return;

        try
        {
            var indices = File.ReadAllLines(mapPath)
                .Select(static line => int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : -1)
                .Where(static i => i >= 0);
            map.LoadPresentPieces(indices);
        }
        catch (IOException)
        {
        }
    }

    private static void PersistMap(CacheSlot slot)
    {
        try
        {
            var lines = slot.Map.SerializePresentPieces()
                .Select(static i => i.ToString(CultureInfo.InvariantCulture));
            File.WriteAllLines(slot.MapPath, lines);
        }
        catch (IOException)
        {
        }
    }

    private static void RefreshEntry(
        FederatedMediaCacheEntry entry,
        Guid peerServerId,
        Guid remoteSessionId,
        string originDirectStreamUrl)
    {
        entry.PeerServerId = peerServerId;
        entry.RemoteSessionId = remoteSessionId;
        entry.OriginDirectStreamUrl = originDirectStreamUrl;
        entry.LastAccessUtc = DateTime.UtcNow;
    }

    private sealed class CacheSlot(
        FederatedMediaCacheEntry entry,
        FederatedMediaPieceMap map,
        string mapPath)
    {
        public FederatedMediaCacheEntry Entry { get; set; } = entry;
        public FederatedMediaPieceMap Map { get; } = map;
        public string MapPath { get; } = mapPath;
        public object MapSync { get; } = new();
        public long PlaybackHeadOffset;
        public int PrefetchRunning;
    }
}
