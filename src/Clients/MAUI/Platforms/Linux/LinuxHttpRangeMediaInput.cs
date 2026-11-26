using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using K7.Clients.MAUI.Playback;
using K7.Clients.Shared.Helpers;
using LibVLCSharp;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// Seekable HTTP input for libvlc that issues the <c>Range</c> requests from .NET
/// (<see cref="HttpClient"/> with the K7 Authorization header). libvlc reads the media through
/// its callbacks access (<c>imem</c>), so Direct Play no longer depends on the libvlc HTTP
/// stack nor on the loopback proxy: inside Flatpak the modern libvlc HTTP module fails to
/// connect ("HTTP connection failure") and VLC falls back to its legacy module, which cannot
/// seek and turns every resume or track switch into a linear read from byte 0.
/// </summary>
internal sealed class LinuxHttpRangeMediaInput : MediaInput
{
    // avformat probes with many small forward skips: drain the current response for those
    // instead of opening a new Range request per skip.
    private const long DrainForwardLimit = 2L * 1024 * 1024;
    private const int HeadersTimeoutSeconds = 30;
    private const int TracedRequests = 12;

    private static readonly HttpClient _http = CreateClient();

    private readonly string _url;
    private readonly Func<string?> _authorization;
    private readonly object _gate = new();
    private HttpResponseMessage? _response;
    private Stream? _body;
    private long _position;
    private long _length = -1;
    private bool _closed;
    private int _requests;

    public LinuxHttpRangeMediaInput(string url, Func<string?> authorization)
    {
        _url = url;
        _authorization = authorization;
        CanSeek = true;
    }

    /// <summary>True once libvlc called <see cref="Close"/>: the instance can be disposed.</summary>
    public bool IsClosed
    {
        get
        {
            lock (_gate)
                return _closed;
        }
    }

    public override bool Open(out ulong size)
    {
        lock (_gate)
        {
            _closed = false;
            if (!OpenAtUnlocked(0))
            {
                size = 0;
                return false;
            }

            size = _length >= 0 ? (ulong)_length : ulong.MaxValue;
            return true;
        }
    }

    public override unsafe int Read(IntPtr buf, uint len)
    {
        lock (_gate)
        {
            if (_closed)
                return -1;
            if (_body is null || len == 0)
                return 0;

            try
            {
                var span = new Span<byte>((void*)buf, (int)Math.Min(len, int.MaxValue));
                var read = _body.Read(span);
                if (read > 0)
                    _position += read;
                return read;
            }
            catch (Exception ex)
            {
                VlcPlayerLog.Warn("vlc http-range read " + ex.GetType().Name + " " + ex.Message);
                return -1;
            }
        }
    }

    public override bool Seek(ulong offset)
    {
        lock (_gate)
        {
            if (_closed)
                return false;

            var target = (long)Math.Min(offset, long.MaxValue);
            if (_length >= 0 && target > _length)
                return false;
            if (target == _position)
                return true;

            if (_body is not null
                && target > _position
                && target - _position <= DrainForwardLimit
                && DrainUnlocked(target - _position))
            {
                return true;
            }

            return OpenAtUnlocked(target);
        }
    }

    public override void Close()
    {
        lock (_gate)
        {
            _closed = true;
            ReleaseUnlocked();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_gate)
                ReleaseUnlocked();
        }

        base.Dispose(disposing);
    }

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        };
        K7ServerSockets.Configure(handler);
        return new HttpClient(handler)
        {
            // Bodies stream for the whole playback; the header wait has its own token.
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private bool OpenAtUnlocked(long offset)
    {
        ReleaseUnlocked();
        HttpResponseMessage? response = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _url);
            request.Headers.Range = new RangeHeaderValue(offset, null);
            var authorization = _authorization();
            if (!string.IsNullOrEmpty(authorization))
                request.Headers.TryAddWithoutValidation("Authorization", authorization);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(HeadersTimeoutSeconds));
            response = _http.Send(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var status = (int)response.StatusCode;
            var bodyStart = offset;
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                var range = response.Content.Headers.ContentRange;
                if (range?.Length is { } total)
                    _length = total;
                else if (_length < 0 && response.Content.Headers.ContentLength is { } remaining)
                    _length = offset + remaining;
                if (range?.From is { } from)
                    bodyStart = from;
            }
            else if (response.StatusCode == HttpStatusCode.OK)
            {
                // The server ignored the Range: the body starts at byte 0.
                if (response.Content.Headers.ContentLength is { } total)
                    _length = total;
                bodyStart = 0;
            }
            else
            {
                VlcPlayerLog.Warn("vlc http-range GET offset=" + offset.ToString(CultureInfo.InvariantCulture) + " status=" + status);
                response.Dispose();
                return false;
            }

            _response = response;
            _body = response.Content.ReadAsStream();
            _position = bodyStart;
            if (_requests++ < TracedRequests)
            {
                VlcPlayerLog.Info(
                    "vlc http-range GET offset=" + offset.ToString(CultureInfo.InvariantCulture)
                    + " status=" + status
                    + " length=" + _length.ToString(CultureInfo.InvariantCulture));
            }

            // A 200 for a mid-file Range: skip to the requested offset.
            if (bodyStart < offset && !DrainUnlocked(offset - bodyStart))
            {
                ReleaseUnlocked();
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc http-range GET offset=" + offset.ToString(CultureInfo.InvariantCulture) + " " + ex.GetType().Name + " " + ex.Message);
            response?.Dispose();
            ReleaseUnlocked();
            return false;
        }
    }

    private bool DrainUnlocked(long count)
    {
        if (_body is null)
            return false;

        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (count > 0)
            {
                var read = _body.Read(buffer, 0, (int)Math.Min(count, buffer.Length));
                if (read <= 0)
                    return false;

                count -= read;
                _position += read;
            }

            return true;
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc http-range drain " + ex.GetType().Name + " " + ex.Message);
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void ReleaseUnlocked()
    {
        try
        {
            _body?.Dispose();
        }
        catch
        {
        }

        try
        {
            _response?.Dispose();
        }
        catch
        {
        }

        _body = null;
        _response = null;
    }
}
