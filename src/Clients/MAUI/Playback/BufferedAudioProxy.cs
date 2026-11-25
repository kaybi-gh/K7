using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace K7.Clients.MAUI.Playback;

/// <summary>
/// Loopback server for one fully downloaded audio file. WebKitGTK marks a progressive
/// response with no finite length as live and then ignores <c>currentTime</c>. A static
/// body with <c>Content-Length</c> and byte ranges seeks like the blob the other clients play.
/// </summary>
internal sealed class BufferedAudioProxy : IDisposable
{
    private readonly byte[] _data;
    private readonly string _contentType;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _traced;
    private bool _disposed;

    public BufferedAudioProxy(byte[] data, string contentType)
    {
        _data = data;
        _contentType = IsSafeMediaType(contentType) ? contentType : "application/octet-stream";
    }

    public string? LocalUrl { get; private set; }

    public bool TryStart()
    {
        if (_disposed)
            return false;

        try
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            LocalUrl = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/audio";
            _cts = new CancellationTokenSource();
            _ = AcceptLoopAsync(_cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("audio-proxy start fail " + ex.GetType().Name + " " + ex.Message);
            Dispose();
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
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
    }

    /// <summary>
    /// Parses one <c>Range</c> value. A null header is the whole resource. Returns false when
    /// the range cannot be satisfied.
    /// </summary>
    internal static bool TryParseRange(long total, string? rangeHeader, out long start, out long length)
    {
        start = 0;
        length = total < 0 ? 0 : total;
        if (string.IsNullOrWhiteSpace(rangeHeader))
            return total >= 0;

        var header = rangeHeader.Trim();
        var equals = header.IndexOf('=');
        if (equals < 0 || !header.AsSpan(0, equals).Trim().Equals("bytes", StringComparison.OrdinalIgnoreCase))
            return false;

        var spec = header[(equals + 1)..].Trim();
        var comma = spec.IndexOf(',');
        if (comma >= 0)
            spec = spec[..comma].Trim();

        var dash = spec.IndexOf('-');
        if (dash < 0 || total <= 0)
            return false;

        var startText = spec[..dash].Trim();
        var endText = spec[(dash + 1)..].Trim();
        if (startText.Length == 0)
        {
            if (!long.TryParse(endText, NumberStyles.None, CultureInfo.InvariantCulture, out var suffix) || suffix <= 0)
                return false;

            if (suffix >= total)
            {
                start = 0;
                length = total;
                return true;
            }

            start = total - suffix;
            length = suffix;
            return true;
        }

        if (!long.TryParse(startText, NumberStyles.None, CultureInfo.InvariantCulture, out start) || start < 0 || start >= total)
            return false;

        long end;
        if (endText.Length == 0)
            end = total - 1;
        else if (!long.TryParse(endText, NumberStyles.None, CultureInfo.InvariantCulture, out end) || end < start)
            return false;

        if (end >= total)
            end = total - 1;

        length = end - start + 1;
        return true;
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
            catch (Exception ex) when (ex is ObjectDisposedException or SocketException)
            {
                client?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                client?.Dispose();
                if (!cancellationToken.IsCancellationRequested)
                    VlcPlayerLog.Warn("audio-proxy accept fail " + ex.GetType().Name);
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
                while (!cancellationToken.IsCancellationRequested)
                {
                    var pending = await TryReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
                    if (pending is not AudioProxyRequest request)
                        return;

                    if (Interlocked.Increment(ref _traced) <= 8)
                    {
                        VlcPlayerLog.Info(
                            "audio-proxy "
                            + request.Method
                            + (request.Range is null ? "" : " range=" + request.Range));
                    }

                    if (request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteHeadersAsync(stream, OptionsHeaders(request.KeepAlive), cancellationToken)
                            .ConfigureAwait(false);
                        if (!request.KeepAlive)
                            return;

                        continue;
                    }

                    if (!request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                        && !request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteHeadersAsync(stream, StatusHeaders(405, "Method Not Allowed", 0, request.KeepAlive), cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }

                    var isHead = request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);
                    if (!TryParseRange(_data.LongLength, request.Range, out var start, out var length))
                    {
                        var headers = StatusHeaders(416, "Range Not Satisfiable", 0, keepAlive: false);
                        headers.Append("Content-Range: bytes */")
                            .Append(_data.LongLength.ToString(CultureInfo.InvariantCulture))
                            .Append("\r\n");
                        await WriteHeadersAsync(stream, headers, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    // Any explicit Range is a 206, including bytes=0- (the whole file). GStreamer
                    // probes with that header and reads the total from Content-Range.
                    var partial = request.Range is not null;
                    var status = partial ? 206 : 200;
                    var reason = partial ? "Partial Content" : "OK";
                    var response = StatusHeaders(status, reason, length, request.KeepAlive);
                    if (partial)
                    {
                        var end = start + length - 1;
                        response.Append("Content-Range: bytes ")
                            .Append(start.ToString(CultureInfo.InvariantCulture))
                            .Append('-')
                            .Append(end.ToString(CultureInfo.InvariantCulture))
                            .Append('/')
                            .Append(_data.LongLength.ToString(CultureInfo.InvariantCulture))
                            .Append("\r\n");
                    }

                    await WriteHeadersAsync(stream, response, cancellationToken).ConfigureAwait(false);
                    if (!isHead && length > 0)
                    {
                        await stream.WriteAsync(_data.AsMemory((int)start, (int)length), cancellationToken)
                            .ConfigureAwait(false);
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (!request.KeepAlive)
                        return;
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
            }
        }
    }

    private StringBuilder StatusHeaders(int status, string reason, long contentLength, bool keepAlive)
    {
        var header = new StringBuilder(256);
        header.Append("HTTP/1.1 ")
            .Append(status.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(reason)
            .Append("\r\n");
        header.Append("Content-Type: ").Append(_contentType).Append("\r\n");
        header.Append("Content-Length: ").Append(contentLength.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        header.Append("Accept-Ranges: bytes\r\n");
        AppendCors(header);
        header.Append(keepAlive ? "Connection: keep-alive\r\n" : "Connection: close\r\n");
        return header;
    }

    private static StringBuilder OptionsHeaders(bool keepAlive)
    {
        var header = new StringBuilder(256);
        header.Append("HTTP/1.1 204 No Content\r\n");
        header.Append("Access-Control-Allow-Methods: GET, HEAD, OPTIONS\r\n");
        header.Append("Access-Control-Allow-Headers: Range, Content-Type\r\n");
        header.Append("Access-Control-Max-Age: 600\r\n");
        AppendCors(header);
        header.Append(keepAlive ? "Connection: keep-alive\r\n" : "Connection: close\r\n");
        return header;
    }

    private static void AppendCors(StringBuilder header)
    {
        header.Append("Access-Control-Allow-Origin: *\r\n");
        header.Append("Access-Control-Expose-Headers: Content-Length, Content-Range, Accept-Ranges, Content-Type\r\n");
    }

    private static async Task WriteHeadersAsync(NetworkStream stream, StringBuilder header, CancellationToken cancellationToken)
    {
        header.Append("\r\n");
        var bytes = Encoding.ASCII.GetBytes(header.ToString());
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AudioProxyRequest?> TryReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var filled = 0;
        while (filled < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (n <= 0)
                return null;

            filled += n;
            var end = IndexOfHeaderEnd(buffer, filled);
            if (end < 0)
                continue;

            var text = Encoding.ASCII.GetString(buffer, 0, end);
            var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
            if (lines.Length == 0 || string.IsNullOrEmpty(lines[0]))
                return null;

            var parts = lines[0].Split(' ');
            if (parts.Length < 2)
                return null;

            string? range = null;
            var keepAlive = true;
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

            return new AudioProxyRequest(parts[0], range, keepAlive);
        }

        return null;
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

    private static bool IsSafeMediaType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > 80)
            return false;

        var slash = contentType.IndexOf('/');
        if (slash <= 0 || slash == contentType.Length - 1)
            return false;

        foreach (var c in contentType)
        {
            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '/' or '+' or '-' or '.')
                continue;

            return false;
        }

        return true;
    }

    private readonly record struct AudioProxyRequest(string Method, string? Range, bool KeepAlive);
}
