using System.Globalization;
using System.Text;
using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Common;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Federation;
using K7.Shared.Enums;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace K7.Server.Application.Services;

public interface IFederatedRequesterHlsService
{
    Task<HttpContentResult?> TryServeAsync(
        Guid localSessionId,
        string path,
        string queryString,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Serves HLS on the requester when the source assigned Peer execution.
/// ffmpeg reads a local piece-cached copy of the origin direct-stream.
/// </summary>
public sealed class FederatedRequesterHlsService(
    IApplicationDbContext context,
    IFederatedPlaybackSessionStore sessionStore,
    IFederatedMediaCache mediaCache,
    IPeerAuthorizationService peerAuthorization,
    IPeerClient peerClient,
    ITranscodeJobManager transcodeJobManager,
    IFfmpegCapabilitiesService ffmpegCapabilitiesService,
    IActiveStreamTracker activeStreamTracker,
    ILogger<FederatedRequesterHlsService> logger) : IFederatedRequesterHlsService
{
    public async Task<HttpContentResult?> TryServeAsync(
        Guid localSessionId,
        string path,
        string queryString,
        CancellationToken cancellationToken = default)
    {
        var session = await context.StreamSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == localSessionId && s.RemoteSessionId != null, cancellationToken);

        if (session is null
            || session.FederatedPlaybackExecution != FederatedPlaybackExecution.Peer
            || session.RemoteIndexedFileId is null
            || session.RemoteSessionId is null
            || session.PeerServerId is null)
        {
            return null;
        }

        // Direct-stream stays on the Range proxy.
        if (path.Equals("direct-stream", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!path.StartsWith("hls-stream/", StringComparison.OrdinalIgnoreCase))
            return null;

        var state = await EnsureStateAsync(session, cancellationToken);
        if (state is null)
            return new EmptyHttpContentResult(404);

        var query = QueryHelpers.ParseQuery(
            queryString.StartsWith('?') ? queryString : "?" + queryString);
        RememberRequestedAudioTrack(state, query);

        try
        {
            await EnsureLocalMediaReadyAsync(state, cancellationToken);
            if (path == "hls-stream/manifest.m3u8")
            {
                CaptureStartSeconds(state, query);
                RememberRequestedBurnIn(state, query);
                return BuildMasterManifest(state, localSessionId, query);
            }

            if (path.StartsWith("hls-stream/video/", StringComparison.Ordinal)
                && path.EndsWith("/index.m3u8", StringComparison.Ordinal))
            {
                var quality = path.Split('/')[2];
                return BuildVideoIndex(state, localSessionId, quality, query);
            }

            if (path.StartsWith("hls-stream/video/", StringComparison.Ordinal)
                && path.Contains("/segments/", StringComparison.Ordinal))
            {
                var segments = path.Split('/');
                var quality = segments[2];
                var segmentFile = Path.GetFileNameWithoutExtension(segments[4]);
                var segmentIndex = segmentFile.Equals("init", StringComparison.OrdinalIgnoreCase)
                    ? -1
                    : int.Parse(segmentFile, CultureInfo.InvariantCulture);
                return await ServeVideoSegmentAsync(
                    state, localSessionId, quality, segmentIndex, query, cancellationToken);
            }

            if (path.StartsWith("hls-stream/audio/", StringComparison.Ordinal)
                && path.EndsWith("/index.m3u8", StringComparison.Ordinal))
            {
                var trackIndex = int.Parse(path.Split('/')[2], CultureInfo.InvariantCulture);
                return BuildAudioIndex(state, localSessionId, trackIndex, query);
            }

            if (path.StartsWith("hls-stream/audio/", StringComparison.Ordinal)
                && path.Contains("/segments/", StringComparison.Ordinal))
            {
                var segments = path.Split('/');
                var trackIndex = int.Parse(segments[2], CultureInfo.InvariantCulture);
                var segmentFile = Path.GetFileNameWithoutExtension(segments[4]);
                var segmentIndex = segmentFile.Equals("init", StringComparison.OrdinalIgnoreCase)
                    ? -1
                    : int.Parse(segmentFile, CultureInfo.InvariantCulture);
                return await ServeAudioSegmentAsync(
                    state, localSessionId, trackIndex, segmentIndex, query, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Federated Peer HLS failed for session {SessionId} path {Path}. Falling back to origin proxy.",
                localSessionId,
                path);
            return null;
        }

        return new EmptyHttpContentResult(404);
    }

    private async Task<FederatedPlaybackSessionState?> EnsureStateAsync(
        Domain.Entities.StreamSession session,
        CancellationToken cancellationToken)
    {
        var existing = sessionStore.Get(session.Id);
        if (existing is not null)
        {
            if (existing.Segments is null)
                await LoadSegmentsAsync(existing, cancellationToken);
            return existing;
        }

        if (session.RemoteSessionId is null
            || session.PeerServerId is null
            || session.RemoteIndexedFileId is null)
        {
            return null;
        }

        var auth = await peerAuthorization.AuthenticateOutboundAsync(session.PeerServerId.Value, cancellationToken);
        if (auth is null)
            return null;

        var (peer, _) = auth.Value;
        var originUrl =
            $"{peer.BaseUrl.TrimEnd('/')}/api/federation/stream-sessions/{session.RemoteSessionId.Value}/direct-stream";

        var remoteFile = await context.RemoteIndexedFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == session.RemoteIndexedFileId.Value, cancellationToken);

        var state = new FederatedPlaybackSessionState
        {
            LocalSessionId = session.Id,
            RemoteSessionId = session.RemoteSessionId.Value,
            PeerServerId = session.PeerServerId.Value,
            RemoteIndexedFileId = session.RemoteIndexedFileId.Value,
            OriginDirectStreamUrl = originUrl,
            StreamDecision = activeStreamTracker.GetStreamInfo(session.Id)?.StreamDecision,
            Duration = remoteFile?.Duration
        };

        sessionStore.Set(state);
        await LoadSegmentsAsync(state, cancellationToken);
        _ = WarmMediaCacheAsync(state);
        return state;
    }

    private async Task EnsureLocalMediaReadyAsync(
        FederatedPlaybackSessionState state,
        CancellationToken cancellationToken)
    {
        var entry = await mediaCache.EnsureOpenedAsync(
            state.RemoteIndexedFileId,
            state.PeerServerId,
            state.RemoteSessionId,
            state.OriginDirectStreamUrl,
            cancellationToken);
        await mediaCache.EnsureHeaderAndCuesAsync(entry, cancellationToken);
        state.LocalMediaPath = entry.LocalPath;
        sessionStore.Set(state);
    }

    private async Task WarmMediaCacheAsync(FederatedPlaybackSessionState state)
    {
        try
        {
            await EnsureLocalMediaReadyAsync(state, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Federated media cache warm-up failed for session {SessionId}",
                state.LocalSessionId);
        }
    }

    private Func<TimeSpan, TimeSpan, CancellationToken, Task> BuildEnsureInputCoverage(
        FederatedPlaybackSessionState state,
        long runwayBytes = 0) =>
        async (windowStart, windowEnd, ct) =>
        {
            if (!mediaCache.TryGet(state.RemoteIndexedFileId, out var entry) || entry is null)
            {
                entry = await mediaCache.EnsureOpenedAsync(
                    state.RemoteIndexedFileId,
                    state.PeerServerId,
                    state.RemoteSessionId,
                    state.OriginDirectStreamUrl,
                    ct);
            }

            await mediaCache.EnsureTimeWindowAsync(
                entry, windowStart, windowEnd, state.Duration, ct, runwayBytes);
        };

    private static long ResolveFederatedRunwayBytes(bool isEncode) =>
        isEncode
            ? FederatedMediaPieceMap.EncodeEnsureRunwayBytes
            : FederatedMediaPieceMap.EnsureRunwayBytes;

    private async Task LoadSegmentsAsync(
        FederatedPlaybackSessionState state,
        CancellationToken cancellationToken)
    {
        var auth = await peerAuthorization.AuthenticateOutboundAsync(state.PeerServerId, cancellationToken);
        if (auth is null)
        {
            logger.LogWarning(
                "Federated Peer HLS session {SessionId}: no peer auth, equal-length fallback",
                state.LocalSessionId);
            ApplyEqualLengthFallback(state);
            return;
        }

        var (peer, token) = auth.Value;

        // Origin may still be finishing ComputeHlsSegments on first play. Poll before the
        // equal-length grid (known A/V desync / reverse-seek path).
        const int maxAttempts = 40;
        const int delayMs = 1500;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var remote = await peerClient.GetRemoteHlsSegmentsAsync(
                peer.BaseUrl, token, state.RemoteSessionId, cancellationToken);

            if (remote.Count > 0)
            {
                state.Segments = remote.Select(ToHlsSegment).ToList();
                state.UsedEqualLengthFallback = false;
                sessionStore.Set(state);
                if (attempt > 0)
                {
                    logger.LogInformation(
                        "Federated Peer HLS session {SessionId}: keyframe grid ready after {Attempts} attempt(s)",
                        state.LocalSessionId,
                        attempt + 1);
                }

                return;
            }

            if (attempt < maxAttempts - 1)
                await Task.Delay(delayMs, cancellationToken);
        }

        logger.LogWarning(
            "Federated Peer HLS session {SessionId}: keyframe grid still empty after wait, equal-length fallback",
            state.LocalSessionId);
        ApplyEqualLengthFallback(state);
    }

    private void ApplyEqualLengthFallback(FederatedPlaybackSessionState state)
    {
        var durationMs = state.Duration is { } d
            ? (long)d.TotalMilliseconds
            : 0L;
        if (durationMs <= 0)
            durationMs = HlsSegmentHelper.TargetSegmentDurationMs;

        state.Segments = HlsSegmentHelper.ComputeEqualLengthHlsSegments(durationMs);
        state.UsedEqualLengthFallback = true;
        if (state.StreamDecision is { Mode: PlaybackMode.Transmux } decision)
        {
            state.StreamDecision = decision with
            {
                Mode = PlaybackMode.Transcode,
                StreamVideoCodec = HlsSegmentHelper.FallbackTranscodingVideoCodec,
                Reason = decision.Reason | TranscodeReason.HlsSegmentsUnavailable
            };
            activeStreamTracker.UpdateStreamDecision(state.LocalSessionId, state.StreamDecision);
        }

        sessionStore.Set(state);
    }

    /// <summary>
    /// PGS is not a text track. The client puts SubtitleBurnInStreamIndex on the master.
    /// A playback URL that omits it means burn-in is off. Segment URLs keep the index
    /// the master already wrote, so they must not clear it.
    /// </summary>
    private void RememberRequestedBurnIn(
        FederatedPlaybackSessionState state,
        Dictionary<string, StringValues> query)
    {
        int fromQuery = -1;
        var hasIndex = query.TryGetValue("SubtitleBurnInStreamIndex", out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out fromQuery)
            && fromQuery >= 0;
        var clientPlaybackUrl = query.ContainsKey("VideoCodecsOnly") || query.ContainsKey("Quality");
        var updated = ApplyFederatedBurnIn(
            state.StreamDecision,
            hasIndex,
            fromQuery,
            clientPlaybackUrl);
        if (ReferenceEquals(updated, state.StreamDecision))
            return;

        state.StreamDecision = updated;
        if (updated is not null)
        {
            activeStreamTracker.UpdateStreamDecision(state.LocalSessionId, updated);
            sessionStore.Set(state);
        }
    }

    /// <summary>
    /// Query index wins. A client playback URL without the index clears burn-in.
    /// A segment URL without it leaves the decision alone.
    /// </summary>
    internal static StreamDecisionDto? ApplyFederatedBurnIn(
        StreamDecisionDto? current,
        bool queryHasBurnIn,
        int burnInIndex,
        bool clientPlaybackUrl)
    {
        if (queryHasBurnIn)
        {
            if (current?.IsSubtitleBurnIn == true && current.SelectedSubtitleTrackIndex == burnInIndex)
                return current;

            var reason = current?.Reason ?? TranscodeReason.None;
            reason |= TranscodeReason.SubtitlesBurnIn;
            return (current ?? new StreamDecisionDto()) with
            {
                Mode = PlaybackMode.Transcode,
                Reason = reason,
                IsSubtitleBurnIn = true,
                SelectedSubtitleTrackIndex = burnInIndex,
                StreamVideoCodec = current?.StreamVideoCodec ?? HlsSegmentHelper.FallbackTranscodingVideoCodec
            };
        }

        if (!clientPlaybackUrl || current is not { IsSubtitleBurnIn: true })
            return current;

        return current with
        {
            IsSubtitleBurnIn = false,
            SelectedSubtitleTrackIndex = null
        };
    }

    private static HlsSegment ToHlsSegment(HlsSegmentDto dto) => new()
    {
        Number = dto.Number,
        StartTimestamp = dto.StartTimestamp,
        Duration = dto.Duration
    };

    /// <summary>
    /// Player selection on the manifest query. Stream 0 is often video, so a missing
    /// index stays null instead of becoming stream 0.
    /// </summary>
    internal static int? ResolveAudioTrackIndex(int? requestedIndex, int? decisionIndex)
    {
        if (requestedIndex is >= 0)
            return requestedIndex.Value;

        if (decisionIndex is >= 0)
            return decisionIndex.Value;

        return null;
    }

    private void RememberRequestedAudioTrack(
        FederatedPlaybackSessionState state,
        Dictionary<string, StringValues> query)
    {
        if (!query.TryGetValue("DefaultAudioTrackIndex", out var raw)
            || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromQuery)
            || fromQuery < 0)
        {
            return;
        }

        state.RequestedAudioTrackIndex = fromQuery;
        if (state.StreamDecision is { } decision
            && decision.SelectedAudioTrackIndex != fromQuery)
        {
            state.StreamDecision = decision with { SelectedAudioTrackIndex = fromQuery };
            activeStreamTracker.UpdateStreamDecision(state.LocalSessionId, state.StreamDecision);
            sessionStore.Set(state);
        }
    }

    private HttpContentResult BuildMasterManifest(
        FederatedPlaybackSessionState state,
        Guid sessionId,
        Dictionary<string, StringValues> query)
    {
        var decision = state.StreamDecision;
        var playlist = new StringBuilder();
        playlist.AppendLine("#EXTM3U");
        playlist.AppendLine("#EXT-X-VERSION:7");
        playlist.AppendLine("#EXT-X-INDEPENDENT-SEGMENTS");

        var audioTrackIndex = ResolveAudioTrackIndex(
            state.RequestedAudioTrackIndex,
            decision?.SelectedAudioTrackIndex) ?? 0;
        var audioCodec = decision?.StreamAudioCodec;
        var needsAudioTranscode = NeedsAudioTranscode(decision);

        var audioChannels = decision?.StreamAudioChannels;
        var audioCodecs = !string.IsNullOrEmpty(audioCodec)
            ? HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, audioCodec: audioCodec)
            : null;

        var startSecondsQuery = FormatStartSecondsQuery(state.StartSeconds);

        playlist.AppendLine(
            $"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio\",NAME=\"Audio\",DEFAULT=YES,AUTOSELECT=YES," +
            $"URI=\"audio/{audioTrackIndex}/index.m3u8?streamSessionId={sessionId}" +
            (needsAudioTranscode && audioCodec is not null
                ? $"&TranscodingAudioCodec={audioCodec}"
                : string.Empty) +
            (audioChannels is > 0
                ? $"&TranscodingAudioChannels={audioChannels.Value.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty) +
            startSecondsQuery +
            "\"");

        var quality = ResolvePlaylistQuality(query, state.StreamDecision);
        var videoCodec = ResolveVideoCodecForQuality(state, quality, videoCodecFromQuery: null);
        decision = state.StreamDecision;

        var burnIn = decision?.IsSubtitleBurnIn == true
            ? decision.SelectedSubtitleTrackIndex
            : null;

        // PGS burn-in cannot stay on a copy. A ladder quality already encodes.
        if (burnIn is not null && string.IsNullOrEmpty(videoCodec))
            videoCodec = HlsSegmentHelper.FallbackTranscodingVideoCodec;

        var bandwidth = 8_000_000;
        if (decision?.Bitrate is > 0)
            bandwidth = decision.Bitrate.Value;

        var videoCodecsOnly = query.TryGetValue("VideoCodecsOnly", out var vco)
            && bool.TryParse(vco, out var onlyVideo)
            && onlyVideo;
        var codecs = ResolveMasterStreamInfCodecs(
            videoCodec,
            decision?.SourceVideoCodec,
            audioCodecs,
            videoCodecsOnly);

        playlist.AppendLine(
            $"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},CODECS=\"{codecs}\",AUDIO=\"audio\",CLOSED-CAPTIONS=NONE");

        var videoParams = new List<string> { $"streamSessionId={sessionId}" };
        if (!string.IsNullOrEmpty(videoCodec))
            videoParams.Add($"TranscodingVideoCodec={videoCodec}");
        if (burnIn is int burn)
            videoParams.Add($"SubtitleBurnInStreamIndex={burn.ToString(CultureInfo.InvariantCulture)}");
        if (state.StartSeconds is > 0)
        {
            videoParams.Add(
                $"startSeconds={state.StartSeconds.Value.ToString("F3", CultureInfo.InvariantCulture)}");
        }

        playlist.AppendLine($"video/{quality}/index.m3u8?{string.Join("&", videoParams)}");

        return new TextHttpContentResult(playlist.ToString(), "application/vnd.apple.mpegurl");
    }

    private HttpContentResult BuildVideoIndex(
        FederatedPlaybackSessionState state,
        Guid sessionId,
        string quality,
        Dictionary<string, StringValues> query)
    {
        var allSegments = ResolveSegments(state);
        var durations = HlsSegmentHelper.ToDurationSeconds(allSegments);
        var videoCodecFromQuery = query.TryGetValue("TranscodingVideoCodec", out var vc) ? vc.ToString() : null;
        var videoCodec = ResolveVideoCodecForQuality(state, quality, videoCodecFromQuery);
        var burnIn = query.TryGetValue("SubtitleBurnInStreamIndex", out var bi)
            && int.TryParse(bi, out var burn)
                ? burn
                : (int?)null;
        var startSeconds = ResolveStartSeconds(state, query);

        var isTransmuxing = string.IsNullOrEmpty(videoCodec) && burnIn is null;

        var queryString = HlsMediaPlaylistBuilder.BuildQueryString(
            sessionId,
            ("TranscodingVideoCodec", videoCodec),
            ("SubtitleBurnInStreamIndex", burnIn?.ToString(CultureInfo.InvariantCulture)));

        var index = HlsMediaPlaylistBuilder.Build(
            durations,
            queryString,
            GetHlsVideoStreamSegmentQueryUriBuilder.BuildPlaylistRelativePath,
            startSeconds,
            independentSegments: !isTransmuxing);

        return new TextHttpContentResult(index, "application/vnd.apple.mpegurl");
    }

    private HttpContentResult BuildAudioIndex(
        FederatedPlaybackSessionState state,
        Guid sessionId,
        int trackIndex,
        Dictionary<string, StringValues> query)
    {
        var allSegments = ResolveSegments(state);
        var durations = HlsSegmentHelper.ToDurationSeconds(allSegments);
        var queryCodec = query.TryGetValue("TranscodingAudioCodec", out var ac) ? ac.ToString() : null;
        var queryChannels = query.TryGetValue("TranscodingAudioChannels", out var ch)
            && int.TryParse(ch, out var channels)
                ? channels
                : (int?)null;
        var (audioCodec, audioChannels) = ResolveFederatedHlsAudio(
            state.StreamDecision,
            queryCodec,
            queryChannels);
        var startSeconds = ResolveStartSeconds(state, query);

        var queryString = HlsMediaPlaylistBuilder.BuildQueryString(
            sessionId,
            ("TranscodingAudioCodec", audioCodec),
            ("TranscodingAudioChannels", audioChannels?.ToString(CultureInfo.InvariantCulture)));

        var index = HlsMediaPlaylistBuilder.Build(
            durations,
            queryString,
            GetHlsAudioStreamSegmentQueryUriBuilder.BuildPlaylistRelativePath,
            startSeconds,
            independentSegments: !string.IsNullOrEmpty(audioCodec));

        return new TextHttpContentResult(index, "application/vnd.apple.mpegurl");
    }

    private async Task<HttpContentResult> ServeVideoSegmentAsync(
        FederatedPlaybackSessionState state,
        Guid sessionId,
        string quality,
        int segmentIndex,
        Dictionary<string, StringValues> query,
        CancellationToken cancellationToken)
    {
        var allSegments = ResolveSegments(state);
        if (segmentIndex >= 0 && segmentIndex >= allSegments.Count)
            return new EmptyHttpContentResult(404);

        var videoCodecFromQuery = query.TryGetValue("TranscodingVideoCodec", out var vc) ? vc.ToString() : null;
        var videoCodec = ResolveVideoCodecForQuality(state, quality, videoCodecFromQuery);
        var burnIn = query.TryGetValue("SubtitleBurnInStreamIndex", out var bi)
            && int.TryParse(bi, out var burn)
                ? burn
                : (int?)null;

        if (string.IsNullOrEmpty(videoCodec)
            && (state.UsedEqualLengthFallback || burnIn is not null))
        {
            videoCodec = state.StreamDecision?.StreamVideoCodec
                ?? HlsSegmentHelper.FallbackTranscodingVideoCodec;
        }

        if (state.StreamDecision is not null)
        {
            var enriched = await StreamDecisionEnrichment.EnrichEncodersAsync(
                state.StreamDecision, ffmpegCapabilitiesService, cancellationToken);
            state.StreamDecision = enriched;
            activeStreamTracker.UpdateStreamDecision(sessionId, enriched);
        }

        var localPath = state.LocalMediaPath
            ?? throw new InvalidOperationException("Federated local media path is not ready.");
        var isVideoEncode = !string.IsNullOrEmpty(videoCodec)
            || burnIn is not null;
        var job = await transcodeJobManager.GetOrStartJobAsync(
            state.RemoteIndexedFileId,
            localPath,
            quality,
            videoCodec,
            audioCodec: null,
            audioTrackIndex: 0,
            isAudioOnly: false,
            sessionId,
            cancellationToken,
            burnIn,
            audioChannels: null,
            ensureInputCoverageAsync: BuildEnsureInputCoverage(
                state, ResolveFederatedRunwayBytes(isVideoEncode)));
        transcodeJobManager.PingJob(job.JobId, sessionId);

        if (isVideoEncode)
            await PrimeFederatedEncodeResumeAsync(state, job, allSegments, cancellationToken);

        if (IsEarlyProbeBehindResumeWindow(job, segmentIndex))
            return new EmptyHttpContentResult(404);

        // Demuxed HLS: video playlist often prefetches harder than audio. Kick the paired
        // audio job to the same index so AAC does not stall while libx264 races ahead.
        if (segmentIndex >= 0 && isVideoEncode)
        {
            _ = KickPairedAudioEnsureAsync(
                state, sessionId, segmentIndex, allSegments, CancellationToken.None);
        }

        var segmentPath = Path.Combine(
            job.OutputDirectory,
            segmentIndex == -1 ? "init.m4s" : $"{segmentIndex}.m4s");

        return await WaitAndReadSegmentAsync(
            segmentPath, job, segmentIndex, allSegments, "video/mp4", cancellationToken);
    }

    private async Task<HttpContentResult> ServeAudioSegmentAsync(
        FederatedPlaybackSessionState state,
        Guid sessionId,
        int trackIndex,
        int segmentIndex,
        Dictionary<string, StringValues> query,
        CancellationToken cancellationToken)
    {
        var allSegments = ResolveSegments(state);
        if (segmentIndex >= 0 && segmentIndex >= allSegments.Count)
            return new EmptyHttpContentResult(404);

        var queryCodec = query.TryGetValue("TranscodingAudioCodec", out var ac) ? ac.ToString() : null;
        var queryChannels = query.TryGetValue("TranscodingAudioChannels", out var ch)
            && int.TryParse(ch, out var channels)
                ? channels
                : (int?)null;
        var (audioCodec, audioChannels) = ResolveFederatedHlsAudio(
            state.StreamDecision,
            queryCodec,
            queryChannels);

        var localPath = state.LocalMediaPath
            ?? throw new InvalidOperationException("Federated local media path is not ready.");
        var isAudioEncode = !string.IsNullOrEmpty(audioCodec);
        var job = await transcodeJobManager.GetOrStartJobAsync(
            state.RemoteIndexedFileId,
            localPath,
            quality: "original",
            videoCodec: null,
            audioCodec,
            trackIndex,
            isAudioOnly: true,
            sessionId,
            cancellationToken,
            subtitleBurnInStreamIndex: null,
            audioChannels,
            ensureInputCoverageAsync: BuildEnsureInputCoverage(
                state, ResolveFederatedRunwayBytes(isAudioEncode)));
        transcodeJobManager.PingJob(job.JobId, sessionId);

        if (isAudioEncode)
            await PrimeFederatedEncodeResumeAsync(state, job, allSegments, cancellationToken);

        if (IsEarlyProbeBehindResumeWindow(job, segmentIndex))
            return new EmptyHttpContentResult(404);

        var segmentPath = Path.Combine(
            job.OutputDirectory,
            segmentIndex == -1 ? "init.m4s" : $"{segmentIndex}.m4s");

        return await WaitAndReadSegmentAsync(
            segmentPath, job, segmentIndex, allSegments, "audio/mp4", cancellationToken);
    }

    private async Task<HttpContentResult> WaitAndReadSegmentAsync(
        string segmentPath,
        TranscodeJob job,
        int segmentNumber,
        List<HlsSegment> allSegments,
        string contentType,
        CancellationToken cancellationToken)
    {
        var requestedIndex = segmentNumber;
        var generationFailure = await HlsSegmentFileWaiter.WaitUntilAvailableAsync(
            segmentPath,
            job,
            ct => transcodeJobManager.EnsureSegmentWillBeGeneratedAsync(job.JobId, requestedIndex, allSegments, ct),
            cancellationToken,
            maxTotalSeconds: segmentNumber == -1 ? 90 : 180);

        if (generationFailure is not null)
        {
            logger.LogError(
                generationFailure,
                "Federated Peer segment {SegmentNumber} failed for job {JobId}",
                segmentNumber,
                job.JobId);
            return new TextHttpContentResult(
                $"Transcoding failed: {generationFailure.Message}",
                "text/plain",
                503);
        }

        await HlsSegmentFileWaiter.WaitUntilReadableAsync(segmentPath, cancellationToken);
        if (!HlsSegmentFileWaiter.TryReadReadySegmentBytes(segmentPath, out var segmentBytes))
        {
            // Brief race: ready check passed then ffmpeg appended again. One more wait.
            await HlsSegmentFileWaiter.WaitUntilReadableAsync(segmentPath, cancellationToken, timeoutSeconds: 10);
            if (!HlsSegmentFileWaiter.TryReadReadySegmentBytes(segmentPath, out segmentBytes))
            {
                return new TextHttpContentResult(
                    "Transcoding failed: segment file is incomplete or corrupt.",
                    "text/plain",
                    503);
            }
        }

        // Same serve-side tfdt rebase as local StreamPlaybackService. Peer encode windows
        // often land with absolute/window PTS (e.g. seg0 at +10s) while audio stays on the
        // playlist - without this, Video.js plays audio-only sync and late video.
        if (segmentNumber >= 0 && segmentNumber < allSegments.Count)
        {
            var initPath = Path.Combine(
                Path.GetDirectoryName(segmentPath) ?? job.OutputDirectory,
                HlsSegmentFileWaiter.InitSegmentFileName);
            var startMs = allSegments[segmentNumber].StartTimestamp;
            var isVideoEncode = IsVideoEncodeJob(job);
            var (rebaseToleranceMs, alignPresentation) = ResolveServeTfdtRebasePolicy(
                job.IsAudioOnly,
                isVideoEncode);
            if (Fmp4TfdtRebase.TryRebaseMediaSegment(
                    segmentBytes,
                    initPath,
                    startMs,
                    out var rebasedBytes,
                    out var rebaseDetail,
                    rebaseToleranceMs,
                    alignPresentationTime: alignPresentation))
            {
                segmentBytes = rebasedBytes;
                logger.LogDebug(
                    "Federated Peer rebased fMP4 tfdt for segment {SegmentNumber} job {JobId}: {Detail}",
                    segmentNumber,
                    job.JobId,
                    rebaseDetail);
                if (!job.IsFfmpegRunning)
                {
                    try
                    {
                        await File.WriteAllBytesAsync(segmentPath, segmentBytes, cancellationToken);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            else if (!rebaseDetail.StartsWith("already-absolute", StringComparison.Ordinal)
                     && rebaseDetail != "skipped")
            {
                logger.LogDebug(
                    "Federated Peer skipped fMP4 tfdt rebase for segment {SegmentNumber} job {JobId}: {Detail}",
                    segmentNumber,
                    job.JobId,
                    rebaseDetail);
            }
        }

        return new BytesHttpContentResult(segmentBytes, contentType);
    }

    private static bool IsVideoEncodeJob(TranscodeJob job)
    {
        if (job.SubtitleBurnInStreamIndex.HasValue)
            return true;

        return !string.IsNullOrEmpty(job.VideoCodec)
            && !string.Equals(job.VideoCodec, "copy", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Same tolerances as local StreamPlaybackService serve rebase.
    /// </summary>
    internal static (int ToleranceMs, bool AlignPresentation) ResolveServeTfdtRebasePolicy(
        bool isAudioOnly,
        bool isVideoEncode)
    {
        if (isAudioOnly)
            return (Hls.TfdtWindowResetThresholdMs, false);

        return (Hls.VideoTfdtRebaseToleranceMs(isVideoEncode), isVideoEncode);
    }

    private static List<HlsSegment> ResolveSegments(FederatedPlaybackSessionState state)
    {
        var keyframe = state.Segments ?? [];
        var totalMs = keyframe.Count > 0
            ? keyframe.Sum(s => s.Duration)
            : state.Duration is { } d
                ? (long)d.TotalMilliseconds
                : HlsSegmentHelper.TargetSegmentDurationMs;
        return HlsSegmentHelper.ResolveVideoStreamingSegments(keyframe, totalMs);
    }

    private static string ResolvePlaylistQuality(
        Dictionary<string, StringValues> query,
        StreamDecisionDto? decision)
    {
        if (query.TryGetValue("Quality", out var q)
            && !StringValues.IsNullOrEmpty(q)
            && !string.IsNullOrWhiteSpace(q.ToString()))
        {
            var requested = q.ToString();
            // Client ladder uses Names like 720p. Ignore WxH leftovers from older peers.
            if (Constants.VideoQualities.Values.Any(v =>
                    string.Equals(v.Name, requested, StringComparison.OrdinalIgnoreCase)))
            {
                return requested;
            }
        }

        if (decision?.Reason.HasFlag(TranscodeReason.QualityDownscale) == true
            && decision.StreamResolution is { } streamRes
            && TryMapResolutionToQualityName(streamRes, out var name))
        {
            return name;
        }

        return "original";
    }

    /// <summary>
    /// Remux has no TranscodingVideoCodec - still advertise <paramref name="sourceVideoCodec"/>
    /// so Video.js loads the video playlist. Web passes VideoCodecsOnly (MSE rejects
    /// combined video+audio type strings).
    /// </summary>
    internal static string ResolveMasterStreamInfCodecs(
        string? transcodingVideoCodec,
        string? sourceVideoCodec,
        string? audioCodecs,
        bool videoCodecsOnly)
    {
        var videoCodecs = !string.IsNullOrEmpty(transcodingVideoCodec)
            ? HlsCodecStringHelpers.GetHlsCodecs(transcodingVideoCodec, audioCodec: null)
            : HlsCodecStringHelpers.GetHlsCodecs(sourceVideoCodec, audioCodec: null);

        if (videoCodecsOnly)
            return !string.IsNullOrEmpty(videoCodecs) ? videoCodecs! : audioCodecs ?? string.Empty;

        return string.Join(",", new[] { videoCodecs, audioCodecs }.Where(c => !string.IsNullOrEmpty(c)));
    }

    /// <summary>
    /// Same contract as local StreamPlaybackService: ladder quality forces encode,
    /// otherwise keep the origin decision (remux video + AAC-only is valid).
    /// </summary>
    private string? ResolveVideoCodecForQuality(
        FederatedPlaybackSessionState state,
        string quality,
        string? videoCodecFromQuery)
    {
        var ladder = Constants.VideoQualities.FirstOrDefault(kvp =>
            string.Equals(kvp.Value.Name, quality, StringComparison.OrdinalIgnoreCase));

        if (ladder.Value is not null
            && !string.Equals(quality, "original", StringComparison.OrdinalIgnoreCase))
        {
            var codec = videoCodecFromQuery ?? HlsSegmentHelper.FallbackTranscodingVideoCodec;
            var updated = StreamDecisionExtensions.ApplyQualityDownscale(
                state.StreamDecision,
                ladder.Value,
                codec,
                state.StreamDecision?.SourceResolution);
            state.StreamDecision = updated;
            activeStreamTracker.UpdateStreamDecision(state.LocalSessionId, updated);
            sessionStore.Set(state);
            return codec;
        }

        return ResolveNonLadderVideoCodec(
            videoCodecFromQuery,
            state.StreamDecision?.Mode,
            state.StreamDecision?.StreamVideoCodec,
            state.UsedEqualLengthFallback);
    }

    /// <summary>
    /// Remux (Transmux) keeps null video codec. Transcode / equal-length fallback force encode.
    /// </summary>
    internal static string? ResolveNonLadderVideoCodec(
        string? videoCodecFromQuery,
        PlaybackMode? mode,
        string? streamVideoCodec,
        bool usedEqualLengthFallback)
    {
        var codec = videoCodecFromQuery;

        if (string.IsNullOrEmpty(codec)
            && mode == PlaybackMode.Transcode
            && !string.IsNullOrEmpty(streamVideoCodec))
        {
            codec = streamVideoCodec;
        }

        if (string.IsNullOrEmpty(codec) && usedEqualLengthFallback)
            codec = HlsSegmentHelper.FallbackTranscodingVideoCodec;

        return codec;
    }

    /// <summary>
    /// HLS audio codec and channel count. A copy decision must not be passed to ffmpeg
    /// as an encode (segment muxer has no AC3 encoder). Direct Play then switched to
    /// HLS asks for AAC without a channel cap, and 5.1 AAC is what WebView2 rejects.
    /// </summary>
    internal static (string? Codec, int? Channels) ResolveFederatedHlsAudio(
        StreamDecisionDto? decision,
        string? queryCodec,
        int? queryChannels)
    {
        var codec = queryCodec;
        if (string.IsNullOrEmpty(codec) && NeedsAudioTranscode(decision))
            codec = decision?.StreamAudioCodec;

        if (string.IsNullOrEmpty(codec))
            return (null, null);

        int? channels = queryChannels is > 0 ? queryChannels : null;
        if (channels is null && decision?.StreamAudioChannels is > 0)
            channels = decision.StreamAudioChannels;

        // Quality downscale rewrites Mode to Transcode but leaves the source codec,
        // so a Direct-only check misses the Windows HLS switch. AAC with no channel
        // count on a copy decision is the Video.js rewrite, which must be stereo.
        if (string.Equals(codec, "aac", StringComparison.OrdinalIgnoreCase)
            && channels is null
            && !NeedsAudioTranscode(decision))
        {
            channels = 2;
        }

        return (codec, channels);
    }

    /// <summary>
    /// Audio job paired with a video encode. AC3 copy into the fMP4 segment muxer
    /// fails the header write, so that path encodes AAC stereo instead.
    /// </summary>
    internal static (string? Codec, int? Channels) ResolvePairedVideoEncodeAudio(StreamDecisionDto? decision)
    {
        var resolved = ResolveFederatedHlsAudio(decision, queryCodec: null, queryChannels: null);
        if (!string.IsNullOrEmpty(resolved.Codec))
            return resolved;

        if (RequiresAacForFmp4(decision?.SourceAudioCodec ?? decision?.StreamAudioCodec))
            return ("aac", 2);

        return resolved;
    }

    internal static bool RequiresAacForFmp4(string? codec)
    {
        if (string.IsNullOrEmpty(codec))
            return false;

        return codec.Equals("ac3", StringComparison.OrdinalIgnoreCase)
            || codec.Equals("eac3", StringComparison.OrdinalIgnoreCase)
            || codec.Equals("truehd", StringComparison.OrdinalIgnoreCase)
            || codec.StartsWith("dts", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeedsAudioTranscode(StreamDecisionDto? decision) =>
        decision?.SourceAudioCodec is not null
        && decision.StreamAudioCodec is not null
        && !string.Equals(
            decision.SourceAudioCodec,
            decision.StreamAudioCodec,
            StringComparison.OrdinalIgnoreCase);

    private void CaptureStartSeconds(
        FederatedPlaybackSessionState state,
        Dictionary<string, StringValues> query)
    {
        if (!query.TryGetValue("startSeconds", out var ss)
            || !double.TryParse(ss, NumberStyles.Float, CultureInfo.InvariantCulture, out var start)
            || start <= 0)
        {
            return;
        }

        state.StartSeconds = start;
        sessionStore.Set(state);
    }

    private static double? ResolveStartSeconds(
        FederatedPlaybackSessionState state,
        Dictionary<string, StringValues> query)
    {
        if (query.TryGetValue("startSeconds", out var ss)
            && double.TryParse(ss, NumberStyles.Float, CultureInfo.InvariantCulture, out var start)
            && start > 0)
        {
            return start;
        }

        return state.StartSeconds is > 0 ? state.StartSeconds : null;
    }

    private static string FormatStartSecondsQuery(double? startSeconds) =>
        startSeconds is > 0
            ? $"&startSeconds={startSeconds.Value.ToString("F3", CultureInfo.InvariantCulture)}"
            : string.Empty;

    /// <summary>
    /// Cold Peer encode on resume: start ffmpeg at the resume segment so init/0 probes do
    /// not burn a full BufferSize window at playlist start while audio is already mid-file.
    /// </summary>
    private async Task PrimeFederatedEncodeResumeAsync(
        FederatedPlaybackSessionState state,
        TranscodeJob job,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        if (state.StartSeconds is not > 0 || allSegments.Count == 0)
            return;

        if (job.WindowStartIndex > 0 || job.IsFfmpegRunning)
            return;

        if (HlsSegmentFileWaiter.IsInitReadyOnDisk(job.OutputDirectory))
            return;

        var resumeIndex = FindSegmentIndexForTime(allSegments, state.StartSeconds.Value);
        if (resumeIndex <= 0)
            return;

        logger.LogInformation(
            "Federated Peer priming encode job {JobId} at resume segment {Resume} (startSeconds={Start})",
            job.JobId,
            resumeIndex,
            state.StartSeconds.Value);

        await transcodeJobManager.EnsureSegmentWillBeGeneratedAsync(
            job.JobId,
            resumeIndex,
            allSegments,
            cancellationToken);
    }

    private static int FindSegmentIndexForTime(IReadOnlyList<HlsSegment> segments, double startSeconds)
    {
        var startMs = (long)(startSeconds * 1000.0);
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            if (segments[i].StartTimestamp <= startMs)
                return i;
        }

        return 0;
    }

    /// <summary>
    /// After resume priming far-seeked to N, HLS may still GET early playlist indices.
    /// Fail fast instead of waiting on generation that would yank the encode window back.
    /// </summary>
    private static bool IsEarlyProbeBehindResumeWindow(TranscodeJob job, int segmentIndex) =>
        segmentIndex >= 0
        && job.WindowStartIndex > 0
        && segmentIndex < job.WindowStartIndex
        && !HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, segmentIndex);

    /// <summary>
    /// Best-effort: start/extend the demuxed audio job toward the same segment the video
    /// client just asked for. Failures must not block video serve.
    /// </summary>
    private async Task KickPairedAudioEnsureAsync(
        FederatedPlaybackSessionState state,
        Guid sessionId,
        int segmentIndex,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        try
        {
            var localPath = state.LocalMediaPath;
            if (localPath is null)
                return;

            var decision = state.StreamDecision;
            var (audioCodec, audioChannels) = ResolvePairedVideoEncodeAudio(decision);

            // Copy-remux audio still needs a job so tips stay paired with video encode.
            var trackIndex = ResolveAudioTrackIndex(
                state.RequestedAudioTrackIndex,
                decision?.SelectedAudioTrackIndex);
            if (trackIndex is not int audioTrackIndex)
                return;
            var isAudioEncode = !string.IsNullOrEmpty(audioCodec);
            var audioJob = await transcodeJobManager.GetOrStartJobAsync(
                state.RemoteIndexedFileId,
                localPath,
                quality: "original",
                videoCodec: null,
                audioCodec,
                audioTrackIndex,
                isAudioOnly: true,
                sessionId,
                cancellationToken,
                subtitleBurnInStreamIndex: null,
                audioChannels,
                ensureInputCoverageAsync: BuildEnsureInputCoverage(
                    state, ResolveFederatedRunwayBytes(isAudioEncode)));
            transcodeJobManager.PingJob(audioJob.JobId, sessionId);

            // Video often probes seg 0 on quality switch after audio already far-seeked.
            // Ensuring behind WindowStart triggers hole-fill / seek-back and A/V desync.
            if (audioJob.WindowStartIndex > 0 && segmentIndex < audioJob.WindowStartIndex)
                return;

            await transcodeJobManager.EnsureSegmentWillBeGeneratedAsync(
                audioJob.JobId,
                segmentIndex,
                allSegments,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(
                ex,
                "Federated paired audio kick failed for session {SessionId} segment {Segment}",
                sessionId,
                segmentIndex);
        }
    }

    private static bool TryMapResolutionToQualityName(string resolution, out string name)
    {
        name = "";
        var parts = resolution.Split('x', 'X');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var width)
            || !int.TryParse(parts[1], out var height))
        {
            return false;
        }

        var match = Constants.VideoQualities.Values.FirstOrDefault(v =>
            v.Width == width && v.Height == height);
        if (match is null)
            return false;

        name = match.Name;
        return true;
    }

}
