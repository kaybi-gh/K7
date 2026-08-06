using System.Diagnostics;
using System.Globalization;
using FFMpegCore;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

public sealed class OpenSubsonicAudioTranscoder(ILogger<OpenSubsonicAudioTranscoder> logger)
    : IOpenSubsonicAudioTranscoder
{
    public Stream OpenProgressiveTranscode(
        string inputFilePath,
        string format,
        int bitrateKbps,
        double timeOffsetSeconds)
    {
        if (string.IsNullOrWhiteSpace(inputFilePath) || !File.Exists(inputFilePath))
            throw new FileNotFoundException("Input file for OpenSubsonic transcode was not found.", inputFilePath);

        var ffmpegPath = GlobalFFOptions.GetFFMpegBinaryPath();
        var bitrate = Math.Clamp(bitrateKbps, 32, 320);
        var offset = Math.Max(0, timeOffsetSeconds);
        var (codecArgs, muxFormat) = ResolveOutput(format, bitrate);

        var args = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-nostdin"
        };

        if (offset > 0)
        {
            args.Add("-ss");
            args.Add(offset.ToString("0.###", CultureInfo.InvariantCulture));
        }

        args.Add("-i");
        args.Add(inputFilePath);
        args.Add("-vn");
        args.AddRange(codecArgs);
        args.Add("-f");
        args.Add(muxFormat);
        args.Add("pipe:1");

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Failed to start ffmpeg for OpenSubsonic transcode ({ffmpegPath}).");
        }

        // Drain stderr so the process cannot block on a full pipe.
        _ = Task.Run(async () =>
        {
            try
            {
                while (await process.StandardError.ReadLineAsync() is { } line)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        logger.LogDebug("OpenSubsonic ffmpeg: {Line}", line);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "OpenSubsonic ffmpeg stderr reader ended");
            }
        });

        logger.LogInformation(
            "OpenSubsonic progressive transcode started: format={Format}, bitrate={Bitrate}kbps, offset={Offset}s, input={Input}",
            format,
            bitrate,
            offset,
            inputFilePath);

        return new FfmpegStdoutStream(process);
    }

    private static (IReadOnlyList<string> CodecArgs, string MuxFormat) ResolveOutput(string format, int bitrateKbps)
    {
        var normalized = format.Trim().ToLowerInvariant();
        return normalized switch
        {
            "aac" => (["-c:a", "aac", "-b:a", $"{bitrateKbps}k"], "adts"),
            "opus" => (["-c:a", "libopus", "-b:a", $"{bitrateKbps}k"], "opus"),
            "ogg" => (["-c:a", "libvorbis", "-b:a", $"{bitrateKbps}k"], "ogg"),
            _ => (["-c:a", "libmp3lame", "-b:a", $"{bitrateKbps}k"], "mp3")
        };
    }

    private sealed class FfmpegStdoutStream(Process process) : Stream
    {
        private readonly Stream _stdout = process.StandardOutput.BaseStream;
        private bool _disposed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _stdout.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _stdout.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _stdout.ReadAsync(buffer, cancellationToken);

        public override void Flush() => _stdout.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;
            if (disposing)
            {
                TryKill();
                _stdout.Dispose();
                process.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            TryKill();
            await _stdout.DisposeAsync();
            process.Dispose();
            await base.DisposeAsync();
        }

        private void TryKill()
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort cleanup when the client disconnects.
            }
        }
    }
}
