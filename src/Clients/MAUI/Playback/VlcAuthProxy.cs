using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.MAUI.Playback;

/// <summary>
/// Loopback HTTP proxy so LibVLC can use its native HTTP stack (Range, cache)
/// while K7 still receives <c>Authorization: Bearer</c>. Direct Play maps every
/// request to one upstream URL. HLS maps <c>/hls/...</c> relatives onto the
/// master playlist directory so video, audio, subtitle playlists and segments
/// keep the Bearer token.
/// </summary>
internal sealed class VlcAuthProxy : IDisposable
{
    private readonly HttpClient _upstream;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private string? _targetUrl;
    private Uri? _hlsDirectory;
    private string? _hlsMasterFileName;
    private bool _hlsMode;
    private bool _disposed;
    private byte[]? _preparedMasterBody;
    private int? _preferredHlsAudioTrackIndex;
    private string? _preparedAudioPlaylistRelative;

    public VlcAuthProxy(string? authorizationHeader)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            MaxConnectionsPerServer = 32
        };
        _upstream = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        _upstream.DefaultRequestHeaders.ExpectContinue = false;
        SetAuthorization(authorizationHeader);
    }

    public void SetAuthorization(string? authorizationHeader)
    {
        _upstream.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrEmpty(authorizationHeader)
            && AuthenticationHeaderValue.TryParse(authorizationHeader, out var auth))
        {
            _upstream.DefaultRequestHeaders.Authorization = auth;
        }
    }

    public string? LocalUrl { get; private set; }

    public string? TargetUrl => _targetUrl;

    public bool IsHls => _hlsMode;

    /// <summary>
    /// Absolute loopback URL of the demuxed audio media playlist captured while
    /// rewriting the master to video-only (Windows LibVLC adaptive never opens
    /// STREAM-INF when an AUDIO group is present).
    /// </summary>
    public string? HlsAudioSlaveUrl { get; private set; }

    /// <summary>
    /// Absolute loopback URL of the video media playlist. LibVLC plays this
    /// directly (not the multivariant master) so demux does not depend on
    /// adaptive STREAM-INF selection.
    /// </summary>
    public string? HlsVideoPlayUrl { get; private set; }

    public string? BuildAudioFmp4Url(int audioTrackIndex, double startSeconds)
    {
        if (LocalUrl is null || !_hlsMode || audioTrackIndex < 0)
            return null;

        if (!Uri.TryCreate(LocalUrl, UriKind.Absolute, out var local))
            return null;

        return "http://127.0.0.1:"
            + local.Port.ToString(CultureInfo.InvariantCulture)
            + "/audio-fmp4?track="
            + audioTrackIndex.ToString(CultureInfo.InvariantCulture)
            + "&start="
            + startSeconds.ToString("F3", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Tiny multivariant master that points at the demuxed audio media playlist.
    /// WinRT AdaptiveMediaSource expects STREAM-INF; a bare media playlist hangs
    /// after the first GET with no MediaOpened / no segments.
    /// </summary>
    /// <summary>
    /// Upstream multivariant master (AUDIO kept) on the loopback proxy, for desktop
    /// VLC diagnostics. Auth is added by the proxy; no access_token needed.
    /// </summary>
    public string? BuildRawMasterUrl()
    {
        if (LocalUrl is null || !_hlsMode)
            return null;

        if (!Uri.TryCreate(LocalUrl, UriKind.Absolute, out var local))
            return null;

        return "http://127.0.0.1:"
            + local.Port.ToString(CultureInfo.InvariantCulture)
            + "/hls/raw-master.m3u8";
    }

    public string? BuildAudioMasterUrl(double startSeconds)
    {
        if (LocalUrl is null || !_hlsMode || string.IsNullOrEmpty(HlsAudioSlaveUrl))
            return null;

        if (!Uri.TryCreate(LocalUrl, UriKind.Absolute, out var local))
            return null;

        return "http://127.0.0.1:"
            + local.Port.ToString(CultureInfo.InvariantCulture)
            + "/audio-master.m3u8?start="
            + Math.Max(0, startSeconds).ToString("F3", CultureInfo.InvariantCulture);
    }

    public bool TryStart(string upstreamUrl)
    {
        if (_disposed || !Uri.TryCreate(upstreamUrl, UriKind.Absolute, out var target))
            return false;

        try
        {
            _targetUrl = upstreamUrl;
            HlsAudioSlaveUrl = null;
            HlsVideoPlayUrl = null;
            _preparedMasterBody = null;
            _preparedAudioPlaylistRelative = null;
            _preferredHlsAudioTrackIndex = null;
            _hlsMode = StreamingSourceKind.IsHls(mimeType: null, upstreamUrl);
            if (_hlsMode)
            {
                var path = target.GetLeftPart(UriPartial.Path);
                var lastSlash = path.LastIndexOf('/');
                if (lastSlash < 0)
                    return false;

                _hlsDirectory = new Uri(path[..(lastSlash + 1)]);
                _hlsMasterFileName = path[(lastSlash + 1)..];
            }
            else
            {
                _hlsDirectory = null;
                _hlsMasterFileName = null;
            }

            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            LocalUrl = _hlsMode
                ? "http://127.0.0.1:" + port + "/hls/" + (_hlsMasterFileName ?? "manifest.m3u8")
                : "http://127.0.0.1:" + port + "/direct";
            _cts = new CancellationTokenSource();
            _ = AcceptLoopAsync(_cts.Token);
            VlcPlayerLog.Info("vlc-proxy listen " + LocalUrl + (_hlsMode ? " hls" : ""));
            return true;
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc-proxy start fail " + ex.GetType().Name + " " + ex.Message);
            StopListener();
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopListener();
        _upstream.Dispose();
    }

    private void StopListener()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
        }

        _cts?.Dispose();
        _cts = null;
        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        _listener = null;
        LocalUrl = null;
        HlsAudioSlaveUrl = null;
        HlsVideoPlayUrl = null;
        _preparedMasterBody = null;
        _preparedAudioPlaylistRelative = null;
        _preferredHlsAudioTrackIndex = null;
    }

    /// <summary>
    /// Fetches the upstream master, rewrites it to video-only for LibVLC, and
    /// sets <see cref="HlsAudioSlaveUrl"/> before <c>MediaPlayer.Play</c> so
    /// <c>AddSlave</c> can attach demuxed audio.
    /// </summary>
    public bool TryPrepareHlsMaster(int? preferredAudioTrackIndex = null)
    {
        if (_disposed || !_hlsMode || string.IsNullOrEmpty(_targetUrl))
            return false;

        try
        {
            _preferredHlsAudioTrackIndex = preferredAudioTrackIndex;
            using var response = VlcProxyUpstream
                .SendAsync(_upstream, "GET", _targetUrl, range: null, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (!response.IsSuccessStatusCode)
            {
                VlcPlayerLog.Warn(
                    "vlc-proxy prepare master "
                    + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
                return false;
            }

            var raw = response.Content.ReadAsStringAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            LogUpstreamHlsAudioDecision(_targetUrl, raw);
            var localBase = TryGetLocalHlsBase();
            var playlist = RewriteHlsMasterForLibVlc(
                raw,
                localBase,
                out var audioSlaveUri,
                out var videoPlayUri,
                preferredAudioTrackIndex);
            _preparedMasterBody = Encoding.UTF8.GetBytes(playlist);
            HlsAudioSlaveUrl = audioSlaveUri;
            HlsVideoPlayUrl = videoPlayUri;
            _preparedAudioPlaylistRelative = TryReadAudioRelativeFromSlaveUri(audioSlaveUri, localBase);
            VlcPlayerLog.Info("vlc-proxy master prepared for libvlc");
            if (!string.IsNullOrEmpty(videoPlayUri))
                VlcPlayerLog.Info("vlc-proxy hls video play " + VlcPlayerLog.SummarizeUrl(videoPlayUri));
            if (!string.IsNullOrEmpty(audioSlaveUri))
            {
                VlcPlayerLog.Info(
                    "vlc-proxy hls audio slave "
                    + VlcPlayerLog.SummarizeUrl(audioSlaveUri)
                    + " hasSession="
                    + (audioSlaveUri.Contains("streamSessionId=", StringComparison.OrdinalIgnoreCase)
                        || audioSlaveUri.Contains("StreamSessionId=", StringComparison.OrdinalIgnoreCase))
                    + " aac="
                    + (audioSlaveUri.Contains("TranscodingAudioCodec=aac", StringComparison.OrdinalIgnoreCase)
                        || audioSlaveUri.Contains("mp4a", StringComparison.OrdinalIgnoreCase)));
            }

            var rawUrl = BuildRawMasterUrl();
            if (!string.IsNullOrEmpty(rawUrl))
                VlcPlayerLog.Info("vlc-proxy desktop-vlc test " + rawUrl);
            if (!string.IsNullOrEmpty(videoPlayUri))
                VlcPlayerLog.Info("vlc-proxy desktop-vlc video-only " + videoPlayUri);
            LogHlsMasterSummary(playlist);
            LogHlsMasterSnippet(playlist);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            VlcPlayerLog.Warn("vlc-proxy prepare master fail " + ex.GetType().Name + " " + ex.Message);
            return false;
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                client?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                client?.Dispose();
                if (!cancellationToken.IsCancellationRequested)
                    VlcPlayerLog.Warn("vlc-proxy accept fail " + ex.GetType().Name);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                // LibVLC adaptive reuses connections for playlist + segments (Android
                // keep-alive). One-shot Connection:close left Length=0 on Windows HLS.
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!TryReadRequest(
                            stream,
                            out var method,
                            out var pathAndQuery,
                            out var range,
                            out var keepAlive))
                        return;

                    if (IsRawMasterPath(pathAndQuery))
                    {
                        await PumpRawMasterAsync(
                                stream,
                                method.Equals("HEAD", StringComparison.OrdinalIgnoreCase),
                                cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }

                    if (IsAudioFmp4Path(pathAndQuery))
                    {
                        await PumpAudioFmp4Async(
                                stream,
                                pathAndQuery,
                                method.Equals("HEAD", StringComparison.OrdinalIgnoreCase),
                                cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }

                    if (IsAudioMasterPath(pathAndQuery))
                    {
                        await PumpAudioMasterAsync(
                                stream,
                                pathAndQuery,
                                method.Equals("HEAD", StringComparison.OrdinalIgnoreCase),
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (!keepAlive)
                            return;
                        continue;
                    }

                    var upstreamUrl = ResolveUpstream(pathAndQuery);
                    if (string.IsNullOrEmpty(upstreamUrl))
                    {
                        VlcPlayerLog.Warn("vlc-proxy resolve miss " + pathAndQuery);
                        return;
                    }

                    // LibVLC adaptive Range-probes fMP4 init (moov @32) and then
                    // discards the audio demux. Serve each HLS object in full.
                    var forwardRange = _hlsMode ? null : range;
                    var isHead = method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);

                    if (_hlsMode && !isHead && IsHlsInitPath(pathAndQuery))
                    {
                        await ForwardHlsInitAsync(stream, method, upstreamUrl, cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }

                    // LibVLC adaptive on Windows never opens STREAM-INF. Proxy still
                    // rewrites masters for diagnostics; playback uses the video media
                    // playlist URL + WinRT AdaptiveMediaSource on /audio-master.m3u8.
                    // Server HLS stays unchanged.
                    if (_hlsMode && !isHead && IsHlsMasterPath(pathAndQuery))
                    {
                        await ForwardHlsMasterAsync(
                                stream,
                                method,
                                upstreamUrl,
                                keepAlive,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (!keepAlive)
                            return;
                        continue;
                    }

                    using var response = await VlcProxyUpstream
                        .SendAsync(_upstream, method, upstreamUrl, forwardRange, cancellationToken)
                        .ConfigureAwait(false);

                    if (_hlsMode)
                    {
                        var code = (int)response.StatusCode;
                        VlcPlayerLog.Info(
                            "vlc-proxy "
                            + method
                            + " "
                            + code.ToString(CultureInfo.InvariantCulture)
                            + " "
                            + SummarizeProxyPath(pathAndQuery));
                        if (code >= 400)
                        {
                            VlcPlayerLog.Warn(
                                "vlc-proxy upstream "
                                + code.ToString(CultureInfo.InvariantCulture)
                                + " "
                                + SummarizeProxyPath(pathAndQuery)
                                + " hasSession="
                                + (pathAndQuery.Contains("streamSessionId=", StringComparison.OrdinalIgnoreCase)
                                    || pathAndQuery.Contains("StreamSessionId=", StringComparison.OrdinalIgnoreCase))
                                + " hasStart="
                                + pathAndQuery.Contains("startSeconds=", StringComparison.OrdinalIgnoreCase));
                        }
                    }

                    var lengthKnown = response.Content.Headers.ContentLength is not null
                        || _hlsMode;
                    var canKeepAlive = keepAlive
                        && !isHead
                        && lengthKnown
                        && response.IsSuccessStatusCode;

                    await WriteResponseAsync(
                            stream,
                            response,
                            isHead,
                            bufferUnknownLength: _hlsMode,
                            acceptByteRanges: !_hlsMode,
                            forceM3u8ContentType: _hlsMode && IsM3u8Path(pathAndQuery),
                            keepAlive: canKeepAlive,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (!canKeepAlive)
                        return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (VlcPlayerLog.IsExpectedClientDisconnect(ex))
                    return;

                VlcPlayerLog.Warn("vlc-proxy forward fail " + ex.GetType().Name + " " + ex.Message);
            }
        }
    }

    private static string SummarizeProxyPath(string pathAndQuery)
    {
        var q = pathAndQuery.IndexOf('?');
        var path = q >= 0 ? pathAndQuery[..q] : pathAndQuery;
        return path.Length <= 80 ? path : path[..80];
    }

    private static bool IsM3u8Path(string pathAndQuery)
    {
        var path = pathAndQuery;
        var q = path.IndexOf('?');
        if (q >= 0)
            path = path[..q];
        return path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRawMasterPath(string pathAndQuery)
    {
        var path = pathAndQuery;
        var query = path.IndexOf('?');
        if (query >= 0)
            path = path[..query];

        return path.Equals("/hls/raw-master.m3u8", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/raw-master.m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PumpRawMasterAsync(
        NetworkStream client,
        bool isHead,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_targetUrl))
        {
            await WriteSimpleStatusAsync(client, 404, "Not Found", cancellationToken).ConfigureAwait(false);
            return;
        }

        using var response = await VlcProxyUpstream
            .SendAsync(_upstream, "GET", _targetUrl, range: null, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            VlcPlayerLog.Warn(
                "vlc-proxy raw-master "
                + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
            await WriteSimpleStatusAsync(client, 502, "Bad Gateway", cancellationToken).ConfigureAwait(false);
            return;
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        // Keep AUDIO (+ strip SUBTITLES only): desktop VLC often stalls on many VTT
        // renditions the same way LibVLC did. Absolutize so relatives resolve on loopback.
        // Drop EXT-X-START: desktop VLC often sits in "opening" forever on mid-film offsets.
        var localBase = TryGetLocalHlsBase();
        var stripped = StripHlsSubtitleRenditions(raw);
        stripped = StripExtXStart(stripped);
        var playlist = FinalizeHlsMasterForLibVlc(stripped, localBase);
        var bytes = Encoding.UTF8.GetBytes(playlist);
        VlcPlayerLog.Info(
            "vlc-proxy raw-master len="
            + bytes.Length.ToString(CultureInfo.InvariantCulture)
            + " audio-kept");

        var header = new StringBuilder(192);
        header.Append("HTTP/1.1 200 OK\r\nContent-Type: application/vnd.apple.mpegurl\r\nContent-Length: ")
            .Append(bytes.Length.ToString(CultureInfo.InvariantCulture))
            .Append("\r\nConnection: close\r\n\r\n");
        await client.WriteAsync(Encoding.ASCII.GetBytes(header.ToString()), cancellationToken)
            .ConfigureAwait(false);
        if (!isHead)
            await client.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await client.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string StripExtXStart(string playlist)
    {
        if (string.IsNullOrEmpty(playlist)
            || !playlist.Contains("#EXT-X-START:", StringComparison.OrdinalIgnoreCase))
        {
            return playlist;
        }

        var sb = new StringBuilder(playlist.Length);
        using var reader = new StringReader(playlist);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("#EXT-X-START:", StringComparison.OrdinalIgnoreCase))
                continue;

            sb.Append(line).Append('\n');
        }

        return sb.ToString();
    }

    private static bool IsAudioFmp4Path(string pathAndQuery)
    {
        var path = pathAndQuery;
        var query = path.IndexOf('?');
        if (query >= 0)
            path = path[..query];

        return path.Equals("/audio-fmp4", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAudioMasterPath(string pathAndQuery)
    {
        var path = pathAndQuery;
        var query = path.IndexOf('?');
        if (query >= 0)
            path = path[..query];

        return path.Equals("/audio-master.m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PumpAudioMasterAsync(
        NetworkStream client,
        string pathAndQuery,
        bool isHead,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HlsAudioSlaveUrl))
        {
            await WriteSimpleStatusAsync(client, 404, "Not Found", cancellationToken).ConfigureAwait(false);
            return;
        }

        var startText = HlsAlternateAudioUrl.TryReadQueryValue(pathAndQuery, "start");
        _ = double.TryParse(startText, NumberStyles.Float, CultureInfo.InvariantCulture, out var startSeconds);
        var audioMediaUrl = EnsureStartSecondsQuery(HlsAudioSlaveUrl, startSeconds);
        var body = BuildAudioOnlyMasterPlaylist(audioMediaUrl);
        var bytes = Encoding.UTF8.GetBytes(body);
        VlcPlayerLog.Info(
            "vlc-proxy audio-master segs-uri "
            + VlcPlayerLog.SummarizeUrl(audioMediaUrl));

        var header = new StringBuilder(192);
        header.Append("HTTP/1.1 200 OK\r\nContent-Type: application/vnd.apple.mpegurl\r\nContent-Length: ")
            .Append(bytes.Length.ToString(CultureInfo.InvariantCulture))
            .Append("\r\nConnection: close\r\n\r\n");
        await client.WriteAsync(Encoding.ASCII.GetBytes(header.ToString()), cancellationToken)
            .ConfigureAwait(false);
        if (!isHead)
            await client.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await client.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Multivariant master with a single STREAM-INF pointing at demuxed audio.
    /// </summary>
    internal static string BuildAudioOnlyMasterPlaylist(string absoluteAudioMediaPlaylistUrl)
    {
        // Omit CODECS - wrong FourCC (e.g. ec-3 vs mp4a) makes WinRT refuse the variant.
        return "#EXTM3U\n"
            + "#EXT-X-VERSION:7\n"
            + "#EXT-X-INDEPENDENT-SEGMENTS\n"
            + "#EXT-X-STREAM-INF:BANDWIDTH=256000,AVERAGE-BANDWIDTH=192000\n"
            + absoluteAudioMediaPlaylistUrl.Trim()
            + "\n";
    }

    private static string EnsureStartSecondsQuery(string url, double startSeconds)
    {
        if (startSeconds <= 0
            || url.Contains("startSeconds=", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return url
            + separator
            + "startSeconds="
            + startSeconds.ToString("F3", CultureInfo.InvariantCulture);
    }

    private async Task PumpAudioFmp4Async(
        NetworkStream client,
        string pathAndQuery,
        bool isHead,
        CancellationToken cancellationToken)
    {
        var trackText = HlsAlternateAudioUrl.TryReadQueryValue(pathAndQuery, "track");
        var startText = HlsAlternateAudioUrl.TryReadQueryValue(pathAndQuery, "start");
        if (!int.TryParse(trackText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var track)
            || track < 0)
        {
            await WriteSimpleStatusAsync(client, 400, "Bad Request", cancellationToken).ConfigureAwait(false);
            return;
        }

        _ = double.TryParse(startText, NumberStyles.Float, CultureInfo.InvariantCulture, out var startSeconds);

        var sessionId = HlsAlternateAudioUrl.TryReadQueryValue(_targetUrl ?? "", "StreamSessionId")
            ?? HlsAlternateAudioUrl.TryReadQueryValue(_targetUrl ?? "", "streamSessionId");
        if (string.IsNullOrEmpty(sessionId) || _hlsDirectory is null)
        {
            await WriteSimpleStatusAsync(client, 404, "Not Found", cancellationToken).ConfigureAwait(false);
            return;
        }

        var playlistPath = !string.IsNullOrEmpty(_preparedAudioPlaylistRelative)
            ? "/hls/" + _preparedAudioPlaylistRelative.TrimStart('/')
            : "/hls/audio/"
                + track.ToString(CultureInfo.InvariantCulture)
                + "/index.m3u8?streamSessionId="
                + sessionId;
        var playlistUrl = ResolveUpstream(playlistPath);
        if (string.IsNullOrEmpty(playlistUrl))
        {
            await WriteSimpleStatusAsync(client, 404, "Not Found", cancellationToken).ConfigureAwait(false);
            return;
        }

        using var playlistResponse = await VlcProxyUpstream
            .SendAsync(_upstream, "GET", playlistUrl, range: null, cancellationToken)
            .ConfigureAwait(false);
        if (!playlistResponse.IsSuccessStatusCode)
        {
            VlcPlayerLog.Warn(
                "vlc-proxy audio-fmp4 playlist "
                + ((int)playlistResponse.StatusCode).ToString(CultureInfo.InvariantCulture));
            await WriteSimpleStatusAsync(client, 502, "Bad Gateway", cancellationToken).ConfigureAwait(false);
            return;
        }

        var playlistText = await playlistResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!HlsMediaPlaylistParser.TryParse(playlistText, playlistUrl, out var mapUrl, out var segments)
            || string.IsNullOrEmpty(mapUrl))
        {
            VlcPlayerLog.Warn("vlc-proxy audio-fmp4 playlist parse fail");
            await WriteSimpleStatusAsync(client, 502, "Bad Gateway", cancellationToken).ConfigureAwait(false);
            return;
        }

        var startIndex = HlsMediaPlaylistParser.FirstSegmentIndexAtOrBefore(segments, startSeconds);
        VlcPlayerLog.Info(
            "vlc-proxy audio-fmp4 track="
            + track.ToString(CultureInfo.InvariantCulture)
            + " start="
            + startSeconds.ToString("F1", CultureInfo.InvariantCulture)
            + "s seg="
            + startIndex.ToString(CultureInfo.InvariantCulture)
            + "/"
            + segments.Count.ToString(CultureInfo.InvariantCulture));

        if (isHead)
        {
            await WriteAudioFmp4HeadersAsync(client, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteAudioFmp4HeadersAsync(client, cancellationToken).ConfigureAwait(false);
        if (!await CopyUpstreamBodyAsync(mapUrl, client, "init", cancellationToken).ConfigureAwait(false))
            return;

        for (var i = startIndex; i < segments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var label = i < startIndex + 3 || i % 50 == 0
                ? "seg-" + i.ToString(CultureInfo.InvariantCulture)
                : null;
            if (!await CopyUpstreamBodyAsync(segments[i].Url, client, label, cancellationToken).ConfigureAwait(false))
                return;
        }

        await client.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> CopyUpstreamBodyAsync(
        string upstreamUrl,
        NetworkStream client,
        string? logLabel,
        CancellationToken cancellationToken)
    {
        using var response = await VlcProxyUpstream
            .SendAsync(_upstream, "GET", upstreamUrl, range: null, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            VlcPlayerLog.Warn(
                "vlc-proxy audio-fmp4 "
                + (logLabel ?? "seg")
                + " "
                + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)
                + " "
                + VlcPlayerLog.SummarizeUrl(upstreamUrl));
            return false;
        }

        if (logLabel is not null)
        {
            VlcPlayerLog.Info(
                "vlc-proxy audio-fmp4 "
                + logLabel
                + " "
                + VlcPlayerLog.SummarizeUrl(upstreamUrl));
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await body.CopyToAsync(client, cancellationToken).ConfigureAwait(false);
        await client.FlushAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task WriteAudioFmp4HeadersAsync(NetworkStream client, CancellationToken cancellationToken)
    {
        // audio/mp4 helps LibVLC treat the progressive fMP4 as an audio elementary stream.
        var header = "HTTP/1.1 200 OK\r\nContent-Type: audio/mp4\r\nConnection: close\r\n\r\n";
        await client.WriteAsync(Encoding.ASCII.GetBytes(header), cancellationToken).ConfigureAwait(false);
        await client.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteSimpleStatusAsync(
        NetworkStream client,
        int status,
        string reason,
        CancellationToken cancellationToken)
    {
        var header = "HTTP/1.0 "
            + status.ToString(CultureInfo.InvariantCulture)
            + " "
            + reason
            + "\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";
        await client.WriteAsync(Encoding.ASCII.GetBytes(header), cancellationToken).ConfigureAwait(false);
    }

    private string? ResolveUpstream(string pathAndQuery)
    {
        if (string.IsNullOrEmpty(_targetUrl))
            return null;

        if (!_hlsMode || _hlsDirectory is null)
            return _targetUrl;

        var relative = pathAndQuery;
        if (relative.StartsWith("/hls/", StringComparison.OrdinalIgnoreCase))
            relative = relative["/hls/".Length..];
        else if (relative.StartsWith("/hls", StringComparison.OrdinalIgnoreCase))
            relative = relative["/hls".Length..].TrimStart('/');
        else
            return _targetUrl;

        if (string.IsNullOrEmpty(relative))
            return _targetUrl;

        var query = "";
        var queryIndex = relative.IndexOf('?');
        var path = relative;
        if (queryIndex >= 0)
        {
            path = relative[..queryIndex];
            query = relative[queryIndex..];
        }

        path = Uri.UnescapeDataString(path);
        if (path.Contains("..", StringComparison.Ordinal))
            return null;

        if (string.Equals(path, _hlsMasterFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "manifest.m3u8", StringComparison.OrdinalIgnoreCase))
        {
            return _targetUrl;
        }

        if (!Uri.TryCreate(_hlsDirectory, path, out var resolved))
            return null;

        if (!resolved.AbsoluteUri.StartsWith(_hlsDirectory.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            return null;

        // LibVLC / desktop VLC often re-request media playlists as bare paths and drop
        // ?streamSessionId=... (seen as upstream 400 hasSession=False). Re-attach session
        // (and useful stream params) from the master target URL.
        return AppendMissingHlsQueryFromTarget(resolved.AbsoluteUri + query);
    }

    private string AppendMissingHlsQueryFromTarget(string upstreamUrl)
    {
        if (string.IsNullOrEmpty(_targetUrl) || string.IsNullOrEmpty(upstreamUrl))
            return upstreamUrl;

        var keys = new[]
        {
            "streamSessionId",
            "StreamSessionId",
            "startSeconds",
            "TranscodingVideoCodec",
            "TranscodingAudioCodec",
        };

        var result = upstreamUrl;
        foreach (var key in keys)
        {
            if (result.Contains(key + "=", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = HlsAlternateAudioUrl.TryReadQueryValue(_targetUrl, key);
            if (string.IsNullOrEmpty(value))
                continue;

            // Prefer lowercase streamSessionId for ASP.NET model binding.
            var emitKey = key.Equals("StreamSessionId", StringComparison.OrdinalIgnoreCase)
                ? "streamSessionId"
                : key;
            if (emitKey.Equals("streamSessionId", StringComparison.Ordinal)
                && result.Contains("streamSessionId=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separator = result.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            result += separator + emitKey + "=" + Uri.EscapeDataString(value);
        }

        // Master carries AudioTrackTranscodings=1:aac; audio media playlists need
        // TranscodingAudioCodec=aac. LibVLC often drops the query from relatives.
        if (IsHlsAudioMediaPlaylistPath(result)
            && !result.Contains("TranscodingAudioCodec=", StringComparison.OrdinalIgnoreCase))
        {
            var fromMap = TryReadTranscodingAudioCodecFromTrackMap(_targetUrl, result);
            if (!string.IsNullOrEmpty(fromMap))
            {
                var separator = result.Contains('?', StringComparison.Ordinal) ? "&" : "?";
                result += separator + "TranscodingAudioCodec=" + Uri.EscapeDataString(fromMap);
            }
        }

        return result;
    }

    private static bool IsHlsAudioMediaPlaylistPath(string url)
    {
        var path = url;
        var query = path.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
            path = path[..query];

        return path.Contains("/audio/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadTranscodingAudioCodecFromTrackMap(string masterUrl, string audioUrl)
    {
        var map = HlsAlternateAudioUrl.TryReadQueryValue(masterUrl, "AudioTrackTranscodings");
        if (string.IsNullOrWhiteSpace(map))
            return null;

        var trackIndex = TryReadAudioTrackIndexFromPath(audioUrl);
        if (trackIndex is null)
            return null;

        foreach (var entry in map.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(':', 2);
            if (parts.Length != 2)
                continue;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                continue;
            if (index == trackIndex.Value && !string.IsNullOrWhiteSpace(parts[1]))
                return parts[1].Trim();
        }

        return null;
    }

    private static int? TryReadAudioTrackIndexFromPath(string url)
    {
        var path = url;
        var query = path.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
            path = path[..query];

        const string marker = "/audio/";
        var start = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += marker.Length;
        var end = path.IndexOf('/', start);
        if (end < 0)
            end = path.Length;

        return int.TryParse(path[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            ? index
            : null;
    }

    private static void LogUpstreamHlsAudioDecision(string? targetUrl, string rawMaster)
    {
        var trackMap = HlsAlternateAudioUrl.TryReadQueryValue(targetUrl ?? "", "AudioTrackTranscodings") ?? "-";
        string? codecs = null;
        string? audioUriSnippet = null;
        using var reader = new StringReader(rawMaster);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("#EXT-X-MEDIA:", StringComparison.Ordinal)
                && line.Contains("TYPE=AUDIO", StringComparison.OrdinalIgnoreCase)
                && audioUriSnippet is null)
            {
                audioUriSnippet = ReadQuotedAttribute(line, "URI=");
                if (audioUriSnippet is { Length: > 96 })
                    audioUriSnippet = audioUriSnippet[..96];
            }

            if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.OrdinalIgnoreCase)
                && codecs is null)
            {
                codecs = ReadQuotedAttribute(line, "CODECS=");
            }
        }

        VlcPlayerLog.Info(
            "vlc-proxy upstream AudioTrackTranscodings="
            + trackMap
            + " CODECS="
            + (codecs ?? "-")
            + " audioUri="
            + (audioUriSnippet ?? "-"));
    }

    private static bool TryReadRequest(
        NetworkStream stream,
        out string method,
        out string pathAndQuery,
        out string? range,
        out bool keepAlive)
    {
        method = "GET";
        pathAndQuery = "/";
        range = null;
        keepAlive = true;
        var buffer = new byte[8192];
        var filled = 0;
        while (filled < buffer.Length)
        {
            var n = stream.Read(buffer, filled, buffer.Length - filled);
            if (n <= 0)
                return false;

            filled += n;
            var end = IndexOfHeaderEnd(buffer, filled);
            if (end < 0)
                continue;

            var text = Encoding.ASCII.GetString(buffer, 0, end);
            var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
            if (lines.Length == 0)
                return false;

            var parts = lines[0].Split(' ');
            if (parts.Length < 2)
                return false;

            method = parts[0];
            pathAndQuery = parts[1];
            if (pathAndQuery.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(pathAndQuery, UriKind.Absolute, out var absolute))
            {
                pathAndQuery = absolute.PathAndQuery;
            }

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
                    range = line[6..].Trim();
                else if (line.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase)
                    && line.Contains("close", StringComparison.OrdinalIgnoreCase))
                {
                    keepAlive = false;
                }
            }

            return true;
        }

        return false;
    }

    private static int IndexOfHeaderEnd(byte[] buffer, int length)
    {
        for (var i = 3; i < length; i++)
        {
            if (buffer[i - 3] == (byte)'\r'
                && buffer[i - 2] == (byte)'\n'
                && buffer[i - 1] == (byte)'\r'
                && buffer[i] == (byte)'\n')
            {
                return i + 1;
            }
        }

        return -1;
    }

    private static bool IsHlsMasterPath(string pathAndQuery)
    {
        var path = pathAndQuery;
        var q = path.IndexOf('?');
        if (q >= 0)
            path = path[..q];

        return path.EndsWith("/manifest.m3u8", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/hls/manifest.m3u8", StringComparison.OrdinalIgnoreCase)
            || path.Equals("manifest.m3u8", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("manifest.m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ForwardHlsMasterAsync(
        NetworkStream client,
        string method,
        string upstreamUrl,
        bool keepAlive,
        CancellationToken cancellationToken)
    {
        byte[] body;
        if (_preparedMasterBody is { Length: > 0 } prepared)
        {
            body = prepared;
            VlcPlayerLog.Info("vlc-proxy GET 200 /hls/manifest.m3u8 (prepared)");
        }
        else
        {
            using var response = await VlcProxyUpstream
                .SendAsync(_upstream, method, upstreamUrl, range: null, cancellationToken)
                .ConfigureAwait(false);

            VlcPlayerLog.Info(
                "vlc-proxy "
                + method
                + " "
                + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)
                + " /hls/manifest.m3u8");

            if (!response.IsSuccessStatusCode)
            {
                await WriteResponseAsync(
                        client,
                        response,
                        isHead: false,
                        bufferUnknownLength: true,
                        acceptByteRanges: false,
                        forceM3u8ContentType: true,
                        keepAlive: false,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var localBase = TryGetLocalHlsBase();
            var playlist = RewriteHlsMasterForLibVlc(
                raw,
                localBase,
                out var audioSlaveUri,
                out var videoPlayUri,
                _preferredHlsAudioTrackIndex);
            HlsAudioSlaveUrl = audioSlaveUri;
            HlsVideoPlayUrl = videoPlayUri;
            _preparedAudioPlaylistRelative = TryReadAudioRelativeFromSlaveUri(audioSlaveUri, localBase);
            if (!string.IsNullOrEmpty(audioSlaveUri))
                VlcPlayerLog.Info("vlc-proxy hls audio slave " + VlcPlayerLog.SummarizeUrl(audioSlaveUri));
            LogHlsMasterSummary(playlist);
            body = Encoding.UTF8.GetBytes(playlist);
            _preparedMasterBody = body;
            VlcPlayerLog.Info("vlc-proxy master rewritten for libvlc");
        }

        var header = new StringBuilder(160);
        header.Append("HTTP/1.1 200 OK\r\nContent-Type: application/vnd.apple.mpegurl\r\nContent-Length: ")
            .Append(body.Length)
            .Append(keepAlive ? "\r\nConnection: keep-alive\r\n\r\n" : "\r\nConnection: close\r\n\r\n");
        await client.WriteAsync(Encoding.ASCII.GetBytes(header.ToString()), cancellationToken)
            .ConfigureAwait(false);
        await client.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await client.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? TryReadAudioRelativeFromSlaveUri(string? absoluteSlaveUri, string? localHlsBase)
    {
        if (string.IsNullOrEmpty(absoluteSlaveUri) || string.IsNullOrEmpty(localHlsBase))
            return null;

        if (!absoluteSlaveUri.StartsWith(localHlsBase, StringComparison.OrdinalIgnoreCase))
            return null;

        return absoluteSlaveUri[localHlsBase.Length..].TrimStart('/');
    }

    private string? TryGetLocalHlsBase()
    {
        if (LocalUrl is null || !Uri.TryCreate(LocalUrl, UriKind.Absolute, out var local))
            return null;

        var path = local.AbsolutePath;
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
            return null;

        return local.GetLeftPart(UriPartial.Authority) + path[..(lastSlash + 1)];
    }

    private static void LogHlsMasterSummary(string playlist)
    {
        var audio = 0;
        var videoUri = "";
        var prevStreamInf = false;
        using var reader = new StringReader(playlist);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("#EXT-X-MEDIA:", StringComparison.Ordinal)
                && line.Contains("TYPE=AUDIO", StringComparison.OrdinalIgnoreCase))
            {
                audio++;
            }

            if (prevStreamInf && !line.StartsWith('#') && line.Length > 0)
            {
                videoUri = line.Length <= 96 ? line : line[..96];
                prevStreamInf = false;
            }

            prevStreamInf = line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal);
        }

        VlcPlayerLog.Info(
            "vlc-proxy master audio="
            + audio.ToString(CultureInfo.InvariantCulture)
            + " video="
            + (string.IsNullOrEmpty(videoUri) ? "-" : videoUri));
    }

    private static void LogHlsMasterSnippet(string playlist)
    {
        var text = playlist.Replace('\r', ' ').Replace('\n', '|');
        if (text.Length > 240)
            text = text[..240];
        VlcPlayerLog.Info("vlc-proxy master body " + text);
    }

    /// <summary>
    /// Prepares the multivariant playlist for LibVLC 4 adaptive on Windows.
    /// Demuxed AUDIO/SUBTITLES are stripped; the video media playlist URI is
    /// returned so the player can open it directly (adaptive never fetched
    /// STREAM-INF). Audio slave URI is the preferred demuxed audio playlist.
    /// </summary>
    internal static string RewriteHlsMasterForLibVlc(
        string playlist,
        string? localHlsBase,
        out string? audioSlaveUri,
        out string? videoPlayUri,
        int? preferredAudioTrackIndex = null)
    {
        var stripped = StripHlsSubtitleRenditions(playlist);
        stripped = StripHlsAudioRenditions(stripped, preferredAudioTrackIndex, out var audioRelative);
        var videoRelative = TryReadFirstStreamInfUri(stripped);
        // Absolute loopback URIs so desktop VLC (and LibVLC) can re-GET without
        // relying on playlist-relative resolution from /hls/manifest.m3u8.
        var finalized = FinalizeHlsMasterForLibVlc(stripped, localHlsBase);
        audioSlaveUri = string.IsNullOrEmpty(audioRelative) || string.IsNullOrEmpty(localHlsBase)
            ? null
            : AbsolutizeHlsRelativeUri(audioRelative, localHlsBase);
        videoPlayUri = string.IsNullOrEmpty(videoRelative) || string.IsNullOrEmpty(localHlsBase)
            ? null
            : AbsolutizeHlsRelativeUri(videoRelative, localHlsBase);
        return finalized;
    }

    private static string? TryReadFirstStreamInfUri(string playlist)
    {
        var prevStreamInf = false;
        using var reader = new StringReader(playlist);
        while (reader.ReadLine() is { } line)
        {
            if (prevStreamInf && !line.StartsWith('#') && line.Length > 0)
                return line.Trim();

            prevStreamInf = line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal);
        }

        return null;
    }

    /// <summary>
    /// Removes HLS subtitle alternate renditions. LibVLC 4 adaptive on Windows
    /// otherwise fetches every VTT playlist/segment and never opens the video variant.
    /// </summary>
    internal static string StripHlsSubtitleRenditions(string playlist)
    {
        if (string.IsNullOrEmpty(playlist)
            || !playlist.Contains("TYPE=SUBTITLES", StringComparison.OrdinalIgnoreCase))
        {
            return playlist;
        }

        var sb = new StringBuilder(playlist.Length);
        using var reader = new StringReader(playlist);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("#EXT-X-MEDIA:", StringComparison.Ordinal)
                && line.Contains("TYPE=SUBTITLES", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal))
                line = StripStreamInfAttribute(line, "SUBTITLES=");

            sb.Append(line).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Removes demuxed AUDIO renditions and returns the preferred audio playlist URI
    /// (DEFAULT=YES, else preferred track index, else first).
    /// </summary>
    internal static string StripHlsAudioRenditions(
        string playlist,
        int? preferredAudioTrackIndex,
        out string? audioUri)
    {
        audioUri = null;
        if (string.IsNullOrEmpty(playlist)
            || !playlist.Contains("TYPE=AUDIO", StringComparison.OrdinalIgnoreCase))
        {
            return playlist;
        }

        string? preferredUri = null;
        string? defaultUri = null;
        string? firstUri = null;

        var sb = new StringBuilder(playlist.Length);
        using var reader = new StringReader(playlist);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("#EXT-X-MEDIA:", StringComparison.Ordinal)
                && line.Contains("TYPE=AUDIO", StringComparison.OrdinalIgnoreCase))
            {
                var uri = ReadQuotedAttribute(line, "URI=");
                if (!string.IsNullOrEmpty(uri))
                {
                    firstUri ??= uri;
                    if (line.Contains("DEFAULT=YES", StringComparison.OrdinalIgnoreCase))
                        defaultUri = uri;
                    if (preferredAudioTrackIndex is int track
                        && uri.Contains(
                            "audio/" + track.ToString(CultureInfo.InvariantCulture) + "/",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        preferredUri = uri;
                    }
                }

                continue;
            }

            if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal))
            {
                line = StripStreamInfAttribute(line, "AUDIO=");
                line = StripAudioFourCcFromCodecs(line);
            }

            sb.Append(line).Append('\n');
        }

        audioUri = preferredUri ?? defaultUri ?? firstUri;
        return sb.ToString();
    }

    private static string? ReadQuotedAttribute(string line, string attributePrefix)
    {
        var start = line.IndexOf(attributePrefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        var valueStart = start + attributePrefix.Length;
        if (valueStart >= line.Length || line[valueStart] != '"')
            return null;

        valueStart++;
        var valueEnd = line.IndexOf('"', valueStart);
        return valueEnd < 0 ? null : line[valueStart..valueEnd];
    }

    private static string StripAudioFourCcFromCodecs(string line)
    {
        const string marker = "CODECS=\"";
        var start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return line;

        var valueStart = start + marker.Length;
        var valueEnd = line.IndexOf('"', valueStart);
        if (valueEnd < 0)
            return line;

        var parts = line[valueStart..valueEnd]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !IsAudioFourCcToken(p))
            .ToArray();

        return line[..valueStart] + string.Join(',', parts) + line[valueEnd..];
    }

    private static bool IsAudioFourCcToken(string token) =>
        token.StartsWith("mp4a.", StringComparison.OrdinalIgnoreCase)
        || token.StartsWith("ac-3", StringComparison.OrdinalIgnoreCase)
        || token.StartsWith("ec-3", StringComparison.OrdinalIgnoreCase)
        || token.Equals("opus", StringComparison.OrdinalIgnoreCase)
        || token.Equals("flac", StringComparison.OrdinalIgnoreCase);

    internal static string FinalizeHlsMasterForLibVlc(string playlist, string? localHlsBase)
    {
        if (string.IsNullOrEmpty(playlist))
            return playlist;

        var hasVersion = playlist.Contains("#EXT-X-VERSION:", StringComparison.OrdinalIgnoreCase);
        var hasIndependent = playlist.Contains(
            "#EXT-X-INDEPENDENT-SEGMENTS",
            StringComparison.OrdinalIgnoreCase);
        var needsAbs = !string.IsNullOrEmpty(localHlsBase);
        var needsCc = playlist.Contains("#EXT-X-STREAM-INF:", StringComparison.OrdinalIgnoreCase)
            && !playlist.Contains("CLOSED-CAPTIONS=", StringComparison.OrdinalIgnoreCase);

        if (hasVersion && hasIndependent && !needsAbs && !needsCc)
            return playlist;

        var sb = new StringBuilder(playlist.Length + 128);
        var afterExtM3u = false;
        var prevStreamInf = false;
        using var reader = new StringReader(playlist);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(line).Append('\n');
                if (!hasVersion)
                    sb.Append("#EXT-X-VERSION:7\n");
                if (!hasIndependent)
                    sb.Append("#EXT-X-INDEPENDENT-SEGMENTS\n");
                afterExtM3u = true;
                prevStreamInf = false;
                continue;
            }

            if (!afterExtM3u)
            {
                sb.Append(line).Append('\n');
                continue;
            }

            if (line.StartsWith("#EXT-X-MEDIA:", StringComparison.Ordinal) && needsAbs)
                line = AbsolutizeHlsMediaUri(line, localHlsBase!);

            if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal))
            {
                if (needsCc && !line.Contains("CLOSED-CAPTIONS=", StringComparison.OrdinalIgnoreCase))
                    line += ",CLOSED-CAPTIONS=NONE";
                prevStreamInf = true;
                sb.Append(line).Append('\n');
                continue;
            }

            if (prevStreamInf && !line.StartsWith('#') && line.Length > 0 && needsAbs)
            {
                line = AbsolutizeHlsRelativeUri(line, localHlsBase!);
                prevStreamInf = false;
            }
            else
            {
                prevStreamInf = false;
            }

            sb.Append(line).Append('\n');
        }

        return sb.ToString();
    }

    private static string AbsolutizeHlsMediaUri(string line, string localHlsBase)
    {
        const string marker = "URI=\"";
        var start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return line;

        var valueStart = start + marker.Length;
        var valueEnd = line.IndexOf('"', valueStart);
        if (valueEnd < 0)
            return line;

        var uri = line[valueStart..valueEnd];
        if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return line;
        }

        return line[..valueStart] + AbsolutizeHlsRelativeUri(uri, localHlsBase) + line[valueEnd..];
    }

    private static string AbsolutizeHlsRelativeUri(string relative, string localHlsBase)
    {
        if (relative.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return relative;
        }

        if (relative.StartsWith("/hls/", StringComparison.OrdinalIgnoreCase))
            return localHlsBase.TrimEnd('/') + relative["/hls".Length..];

        return localHlsBase.TrimEnd('/') + "/" + relative.TrimStart('/');
    }

    private static string StripStreamInfAttribute(string line, string attributePrefix)
    {
        var start = line.IndexOf(attributePrefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return line;

        var valueStart = start + attributePrefix.Length;
        var valueEnd = valueStart;
        if (valueEnd < line.Length && line[valueEnd] == '"')
        {
            valueEnd++;
            while (valueEnd < line.Length && line[valueEnd] != '"')
                valueEnd++;
            if (valueEnd < line.Length)
                valueEnd++;
        }
        else
        {
            while (valueEnd < line.Length && line[valueEnd] != ',')
                valueEnd++;
        }

        var removeFrom = start;
        if (removeFrom > 0 && line[removeFrom - 1] == ',')
            removeFrom--;
        else if (valueEnd < line.Length && line[valueEnd] == ',')
            valueEnd++;

        return line[..removeFrom] + line[valueEnd..];
    }

    private static bool IsHlsInitPath(string pathAndQuery)
    {
        var path = pathAndQuery;
        var query = path.IndexOf('?');
        if (query >= 0)
            path = path[..query];

        return path.EndsWith("/init.m4s", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("init.m4s", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ForwardHlsInitAsync(
        NetworkStream client,
        string method,
        string upstreamUrl,
        CancellationToken cancellationToken)
    {
        byte[]? body = null;
        HttpResponseMessage? last = null;
        var deadline = DateTime.UtcNow.AddSeconds(60);
        var attempt = 0;
        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            last?.Dispose();
            last = await VlcProxyUpstream
                .SendAsync(_upstream, method, upstreamUrl, range: null, cancellationToken)
                .ConfigureAwait(false);
            if (!last.IsSuccessStatusCode)
            {
                VlcPlayerLog.Warn(
                    "vlc-proxy hls init "
                    + ((int)last.StatusCode).ToString(CultureInfo.InvariantCulture)
                    + " "
                    + VlcPlayerLog.SummarizeUrl(upstreamUrl));
                break;
            }

            body = await last.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (!IsTruncatedFmp4Init(body))
                break;

            VlcPlayerLog.Warn(
                "vlc-proxy hls init truncated len="
                + body.Length.ToString(CultureInfo.InvariantCulture)
                + " try="
                + attempt.ToString(CultureInfo.InvariantCulture)
                + " "
                + VlcPlayerLog.SummarizeUrl(upstreamUrl));
            body = null;
            await Task.Delay(Math.Min(2000, 400 * attempt), cancellationToken).ConfigureAwait(false);
        }

        if (last is null)
            return;

        using (last)
        {
            if (body is null)
            {
                // Do not hand LibVLC a 453-byte ftyp+empty moov. That kills the
                // adaptive demux (no audio ES, SetTime treated as MP4).
                await WriteSimpleStatusAsync(client, 503, "Service Unavailable", cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            VlcPlayerLog.Info(
                "vlc-proxy hls init len="
                + body.Length.ToString(CultureInfo.InvariantCulture)
                + " "
                + VlcPlayerLog.SummarizeUrl(upstreamUrl));
            var header = new StringBuilder(128);
            header.Append("HTTP/1.1 200 OK\r\nContent-Type: video/mp4\r\nContent-Length: ")
                .Append(body.Length)
                .Append("\r\nConnection: close\r\n\r\n");
            await client.WriteAsync(Encoding.ASCII.GetBytes(header.ToString()), cancellationToken)
                .ConfigureAwait(false);
            await client.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            await client.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTruncatedFmp4Init(byte[] data)
    {
        if (data.Length < 48)
            return true;

        if (data[4] != (byte)'f'
            || data[5] != (byte)'t'
            || data[6] != (byte)'y'
            || data[7] != (byte)'p')
        {
            return true;
        }

        var ftypSize = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
        if (ftypSize < 8 || ftypSize + 8 > data.Length)
            return true;

        var next = ftypSize;
        var boxSize = (data[next] << 24)
            | (data[next + 1] << 16)
            | (data[next + 2] << 8)
            | data[next + 3];
        return boxSize == 0 || next + boxSize > data.Length;
    }

    private static async Task WriteResponseAsync(
        NetworkStream client,
        HttpResponseMessage response,
        bool isHead,
        bool bufferUnknownLength,
        bool acceptByteRanges,
        bool forceM3u8ContentType,
        bool keepAlive,
        CancellationToken cancellationToken)
    {
        await using var body = isHead
            ? Stream.Null
            : await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        byte[]? buffered = null;
        var length = response.Content.Headers.ContentLength;
        if (!isHead && length is null && bufferUnknownLength)
        {
            using var memory = new MemoryStream();
            await body.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            buffered = memory.ToArray();
            length = buffered.Length;
        }

        var header = new StringBuilder(256);
        header.Append("HTTP/1.1 ")
            .Append((int)response.StatusCode)
            .Append(' ')
            .Append(string.IsNullOrEmpty(response.ReasonPhrase) ? "OK" : response.ReasonPhrase)
            .Append("\r\n");

        if (length is long contentLength)
            header.Append("Content-Length: ").Append(contentLength).Append("\r\n");
        if (response.Content.Headers.ContentRange is { } contentRange)
            header.Append("Content-Range: ").Append(contentRange).Append("\r\n");

        if (forceM3u8ContentType)
            header.Append("Content-Type: application/vnd.apple.mpegurl\r\n");
        else if (response.Content.Headers.ContentType is { } contentType)
            header.Append("Content-Type: ").Append(contentType).Append("\r\n");

        if (acceptByteRanges)
            header.Append("Accept-Ranges: bytes\r\n");
        header.Append(keepAlive ? "Connection: keep-alive\r\n\r\n" : "Connection: close\r\n\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        await client.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

        if (isHead)
            return;

        if (buffered is not null)
            await client.WriteAsync(buffered, cancellationToken).ConfigureAwait(false);
        else
            await body.CopyToAsync(client, cancellationToken).ConfigureAwait(false);

        await client.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
