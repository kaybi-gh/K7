#if WINDOWS
using System.Globalization;
using K7.Clients.MAUI.Playback;
using K7.Clients.Shared.Helpers;
using NAudio.Wave;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// Demuxed HLS audio for Windows LibVLC video. LibVLC and WinRT AdaptiveMediaSource
/// both fail to pull audio-only fMP4 segments. This sidecar downloads init+segments
/// from the loopback proxy, decodes each with Media Foundation, and plays PCM via WASAPI.
/// </summary>
/// <summary>
/// Dead path: Windows HLS uses Video.js, not LibVLC. Kept only while
/// <see cref="WindowsVlcVideoPlayer"/> still contains unused HLS branches.
/// </summary>
internal sealed class WindowsHlsAudioSidecar : IDisposable
{
    private const double MaxBufferedSeconds = 18;
    private static readonly HttpClient Http = CreateHttpClient();

    private CancellationTokenSource? _pumpCts;
    private WasapiOut? _output;
    private BufferedWaveProvider? _buffer;
    private double _volume01 = 1;
    private bool _muted;
    private double _rate = 1;
    private bool _started;
    private bool _disposed;
    private int _playEpoch;

    public bool IsActive => !_disposed && _started;

    public void Play(string mediaPlaylistUrl, double startSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(mediaPlaylistUrl))
            return;

        StopPump();
        _started = true;
        var epoch = Interlocked.Increment(ref _playEpoch);
        var cts = new CancellationTokenSource();
        _pumpCts = cts;
        VlcPlayerLog.Info(
            "win-audio play "
            + VlcPlayerLog.SummarizeUrl(mediaPlaylistUrl)
            + " start="
            + Math.Max(0, startSeconds).ToString("F1", CultureInfo.InvariantCulture)
            + "s via=mf-segments");
        _ = PumpAsync(mediaPlaylistUrl, Math.Max(0, startSeconds), epoch, cts.Token);
    }

    public void Resume()
    {
        try
        {
            _output?.Play();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("win-audio resume fail " + ex.GetType().Name);
        }
    }

    public void Pause()
    {
        try
        {
            _output?.Pause();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("win-audio pause fail " + ex.GetType().Name);
        }
    }

    public void Seek(double seconds)
    {
        // Restart the segment pump at the new timeline (MF buffer is not seekable).
        if (!_started || _disposed)
            return;

        // Caller re-invokes Play with the playlist URL; Seek alone cannot recover the URL.
        VlcPlayerLog.Info(
            "win-audio seek deferred start="
            + Math.Max(0, seconds).ToString("F1", CultureInfo.InvariantCulture)
            + "s");
    }

    public void SeekTo(string mediaPlaylistUrl, double seconds) =>
        Play(mediaPlaylistUrl, seconds);

    public void SetVolume(double volume01, bool muted)
    {
        _volume01 = Math.Clamp(volume01, 0, 1);
        _muted = muted;
        ApplyVolume();
    }

    public void SetRate(double rate)
    {
        // Segment PCM pump plays at 1x; LibVLC video rate still applies to picture.
        _rate = Math.Clamp(rate, 0.25, 4.0);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopPump();
    }

    private async Task PumpAsync(
        string mediaPlaylistUrl,
        double startSeconds,
        int epoch,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "k7-win-audio-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".mp4");
        try
        {
            using var playlistResponse = await Http
                .GetAsync(mediaPlaylistUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!playlistResponse.IsSuccessStatusCode)
            {
                VlcPlayerLog.Warn(
                    "win-audio playlist "
                    + ((int)playlistResponse.StatusCode).ToString(CultureInfo.InvariantCulture));
                MarkFailed(epoch);
                return;
            }

            var playlistText = await playlistResponse.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!HlsMediaPlaylistParser.TryParse(
                    playlistText,
                    mediaPlaylistUrl,
                    out var mapUrl,
                    out var segments)
                || string.IsNullOrEmpty(mapUrl)
                || segments.Count == 0)
            {
                VlcPlayerLog.Warn("win-audio playlist parse fail");
                MarkFailed(epoch);
                return;
            }

            var startIndex = HlsMediaPlaylistParser.FirstSegmentIndexAtOrBefore(segments, startSeconds);
            VlcPlayerLog.Info(
                "win-audio segments "
                + startIndex.ToString(CultureInfo.InvariantCulture)
                + "/"
                + segments.Count.ToString(CultureInfo.InvariantCulture));

            var initBytes = await Http
                .GetByteArrayAsync(new Uri(mapUrl), cancellationToken)
                .ConfigureAwait(false);
            if (initBytes.Length == 0)
            {
                VlcPlayerLog.Warn("win-audio init empty");
                MarkFailed(epoch);
                return;
            }

            VlcPlayerLog.Info(
                "win-audio init len="
                + initBytes.Length.ToString(CultureInfo.InvariantCulture));

            for (var i = startIndex; i < segments.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (epoch != _playEpoch || _disposed)
                    return;

                await WaitForBufferRoomAsync(cancellationToken).ConfigureAwait(false);

                var segmentBytes = await Http
                    .GetByteArrayAsync(new Uri(segments[i].Url), cancellationToken)
                    .ConfigureAwait(false);
                if (segmentBytes.Length == 0)
                {
                    VlcPlayerLog.Warn(
                        "win-audio seg empty i="
                        + i.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                await File.WriteAllBytesAsync(
                        tempPath,
                        Concat(initBytes, segmentBytes),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!TryDecodeSegmentToBuffer(tempPath, epoch, i, cancellationToken))
                    continue;

                if (i == startIndex || i % 40 == 0)
                {
                    VlcPlayerLog.Info(
                        "win-audio seg "
                        + i.ToString(CultureInfo.InvariantCulture)
                        + " buf="
                        + (_buffer?.BufferedDuration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)
                            ?? "-")
                        + "s");
                }
            }

            VlcPlayerLog.Info("win-audio pump done");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("win-audio pump fail " + ex.GetType().Name + " " + ex.Message);
            MarkFailed(epoch);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
            }
        }
    }

    private bool TryDecodeSegmentToBuffer(
        string tempPath,
        int epoch,
        int segmentIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new MediaFoundationReader(tempPath);
            EnsureOutput(reader.WaveFormat, epoch);
            if (_buffer is null || epoch != _playEpoch)
                return false;

            var bytes = new byte[reader.WaveFormat.AverageBytesPerSecond / 2];
            int read;
            while ((read = reader.Read(bytes, 0, bytes.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _buffer.AddSamples(bytes, 0, read);
            }

            return true;
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn(
                "win-audio decode fail i="
                + segmentIndex.ToString(CultureInfo.InvariantCulture)
                + " "
                + ex.GetType().Name
                + " "
                + ex.Message);
            return false;
        }
    }

    private void EnsureOutput(WaveFormat format, int epoch)
    {
        if (_buffer is not null && _output is not null)
            return;
        if (epoch != _playEpoch || _disposed)
            return;

        _buffer = new BufferedWaveProvider(format)
        {
            BufferDuration = TimeSpan.FromSeconds(MaxBufferedSeconds + 6),
            DiscardOnBufferOverflow = false,
        };
        _output = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 50);
        _output.Init(_buffer);
        ApplyVolume();
        _output.Play();
        VlcPlayerLog.Info(
            "win-audio wasapi "
            + format.SampleRate.ToString(CultureInfo.InvariantCulture)
            + "Hz ch="
            + format.Channels.ToString(CultureInfo.InvariantCulture));
    }

    private async Task WaitForBufferRoomAsync(CancellationToken cancellationToken)
    {
        while (_buffer is not null
            && _buffer.BufferedDuration.TotalSeconds > MaxBufferedSeconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }
    }

    private void ApplyVolume()
    {
        if (_output is null)
            return;

        try
        {
            _output.Volume = _muted ? 0f : (float)_volume01;
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("win-audio volume fail " + ex.GetType().Name);
        }
    }

    private void MarkFailed(int epoch)
    {
        if (epoch == _playEpoch)
            _started = false;
    }

    private void StopPump()
    {
        try
        {
            _pumpCts?.Cancel();
        }
        catch
        {
        }

        _pumpCts?.Dispose();
        _pumpCts = null;

        try
        {
            _output?.Stop();
        }
        catch
        {
        }

        _output?.Dispose();
        _output = null;
        _buffer = null;
        _started = false;
    }

    private static byte[] Concat(byte[] init, byte[] segment)
    {
        var combined = new byte[init.Length + segment.Length];
        Buffer.BlockCopy(init, 0, combined, 0, init.Length);
        Buffer.BlockCopy(segment, 0, combined, init.Length, segment.Length);
        return combined;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "K7-win-audio");
        return client;
    }
}
#endif
