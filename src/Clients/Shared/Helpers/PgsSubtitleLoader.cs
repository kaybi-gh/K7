using K7.Shared.QueryBuilders;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Opens one window of a .sup track. Null means the track cannot be served at all (no retry);
/// a throw is a transient failure.
/// </summary>
public delegate Task<Stream?> PgsWindowSource(
    double positionSeconds,
    double durationSeconds,
    CancellationToken cancellationToken);

/// <summary>
/// Feeds a <see cref="PgsTimeline"/> from positioned windows of the server .sup sidecar, so a
/// seek far into a file never downloads the whole track. Windows that extend the loaded range
/// keep the parser (and its epoch state); a seek outside it starts a new parser, so a screen
/// whose epoch began before the window shows from the next epoch start.
/// </summary>
public sealed class PgsSubtitleLoader : IDisposable
{
    public const double WindowSeconds = 120;
    public const double LookBehindSeconds = 10;
    public const double PrefetchSeconds = 45;
    public const int MaxConsecutiveFailures = 3;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(4);
    private static readonly HttpRequestOptionsKey<bool> BrowserStreamingKey = new("WebAssemblyEnableStreamingResponse");

    private readonly Lock _gate = new();
    private readonly PgsTimeline _timeline = new();
    private readonly PgsWindowSource _source;
    private readonly Func<DateTime> _clock;
    private PgsParser _parser = new();
    private CancellationTokenSource? _loadCts;
    private bool _loading;
    private bool _hasRange;
    private double _rangeStart;
    private double _rangeEnd;
    private int _failures;
    private DateTime _retryAfterUtc = DateTime.MinValue;
    private bool _unavailable;
    private bool _disposed;

    public PgsSubtitleLoader(PgsWindowSource source, Func<DateTime>? clock = null)
    {
        _source = source;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>Raised when the loaded screens were dropped (seek outside the loaded range).</summary>
    public event Action? Reset;

    /// <summary>Raised for each screen as it is parsed. May fire off the caller's thread.</summary>
    public event Action<PgsFrame>? FrameLoaded;

    /// <summary>Raised when the time up to which screens are known moves forward.</summary>
    public event Action<double>? LoadedUntilChanged;

    /// <summary>True once the server said the track cannot be served.</summary>
    public bool IsUnavailable
    {
        get
        {
            lock (_gate)
                return _unavailable;
        }
    }

    /// <summary>
    /// Window source backed by the K7 .sup endpoint of an indexed file.
    /// </summary>
    public static PgsWindowSource ForServer(
        HttpClient httpClient,
        Func<string, Uri?> toAbsoluteUri,
        Guid indexedFileId,
        int subtitleTrackIndex)
    {
        return async (position, duration, cancellationToken) =>
        {
            var relative = GetIndexedFileSubtitleSupQueryUriBuilder.Build(
                indexedFileId, subtitleTrackIndex, position, duration);
            var uri = toAbsoluteUri(relative);
            if (uri is null)
                return null;

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            // Browser fetch buffers the whole body unless asked to stream it.
            request.Options.Set(BrowserStreamingKey, true);
            var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var status = (int)response.StatusCode;
            if (status is 400 or 401 or 403 or 404)
            {
                response.Dispose();
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                throw new HttpRequestException("PGS window request failed with status " + status);
            }

            return new ResponseStream(response, await response.Content.ReadAsStreamAsync(cancellationToken));
        };
    }

    /// <summary>
    /// The screen to draw at <paramref name="positionSeconds"/>, or null for an empty plane.
    /// </summary>
    public PgsFrame? FrameAt(double positionSeconds)
    {
        lock (_gate)
            return _timeline.FrameAt(positionSeconds);
    }

    /// <summary>
    /// Call with the player clock: loads the window around it, extends it ahead of the
    /// playhead, and starts over after a seek outside the loaded range.
    /// </summary>
    public void Update(double positionSeconds)
    {
        if (!double.IsFinite(positionSeconds) || positionSeconds < 0)
            return;

        double start;
        PgsParser parser;
        CancellationToken token;
        var reset = false;

        lock (_gate)
        {
            if (_disposed || _unavailable)
                return;

            var outside = !_hasRange
                || positionSeconds < _rangeStart - 0.5
                || positionSeconds > _rangeEnd + 1;
            if (outside)
            {
                if (_clock() < _retryAfterUtc)
                    return;

                _loadCts?.Cancel();
                _loadCts?.Dispose();
                _loadCts = null;
                _loading = false;
                _timeline.Reset();
                _parser = new PgsParser();
                _hasRange = true;
                _rangeStart = Math.Max(0, positionSeconds - LookBehindSeconds);
                _rangeEnd = _rangeStart;
                reset = true;
            }
            else
            {
                _timeline.Prune(positionSeconds);
                if (_loading
                    || positionSeconds < _rangeEnd - PrefetchSeconds
                    || _clock() < _retryAfterUtc)
                {
                    return;
                }
            }

            start = _rangeEnd;
            _rangeEnd = start + WindowSeconds;
            _loading = true;
            _loadCts = new CancellationTokenSource();
            token = _loadCts.Token;
            parser = _parser;
        }

        if (reset)
            Reset?.Invoke();

        _ = LoadWindowAsync(start, parser, token);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
            _timeline.Reset();
        }
    }

    private async Task LoadWindowAsync(double start, PgsParser parser, CancellationToken cancellationToken)
    {
        var failed = false;
        try
        {
            await using var stream = await _source(start, WindowSeconds, cancellationToken).ConfigureAwait(false);
            if (stream is null)
            {
                lock (_gate)
                {
                    if (!cancellationToken.IsCancellationRequested)
                        _unavailable = true;
                }

                return;
            }

            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                var frames = parser.Feed(buffer.AsSpan(0, read));
                foreach (var frame in frames)
                    Publish(frame, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            failed = true;
        }

        double loadedUntil;
        lock (_gate)
        {
            if (cancellationToken.IsCancellationRequested || _disposed)
                return;

            _loading = false;
            if (failed)
            {
                // Give the range back so the next tick asks for this window again.
                _rangeEnd = start;
                _failures++;
                if (_failures >= MaxConsecutiveFailures)
                    _unavailable = true;
                else
                    _retryAfterUtc = _clock() + RetryDelay;

                return;
            }

            _failures = 0;
            _timeline.MarkLoaded(start + WindowSeconds);
            loadedUntil = _timeline.LoadedUntilSeconds;
        }

        LoadedUntilChanged?.Invoke(loadedUntil);
    }

    private void Publish(PgsFrame frame, CancellationToken cancellationToken)
    {
        double loadedUntil;
        lock (_gate)
        {
            if (cancellationToken.IsCancellationRequested || _disposed)
                return;

            _timeline.Add(frame);
            loadedUntil = _timeline.LoadedUntilSeconds;
        }

        FrameLoaded?.Invoke(frame);
        LoadedUntilChanged?.Invoke(loadedUntil);
    }

    /// <summary>Keeps the HTTP response alive for as long as its body is read.</summary>
    private sealed class ResponseStream(HttpResponseMessage response, Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
