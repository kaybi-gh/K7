using FFMpegCore;
using FFMpegCore.Pipes;
using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

public class FfmpegWaveformGenerator(
    ILogger<FfmpegWaveformGenerator> logger) : IWaveformGenerator
{
    public async Task<float[]?> GenerateAsync(string filePath, int peakCount = 200, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            logger.LogWarning("Waveform generation skipped: file not found at '{FilePath}'", filePath);
            return null;
        }

        try
        {
            var samples = await ExtractRawSamples(filePath, cancellationToken);
            if (samples is null || samples.Length == 0)
                return null;

            return ComputePeaks(samples, peakCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Waveform generation failed for '{FilePath}'", filePath);
            return null;
        }
    }

    private static async Task<float[]?> ExtractRawSamples(string filePath, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();

        var success = await FFMpegArguments
            .FromFileInput(filePath, verifyExists: false)
            .OutputToPipe(new StreamPipeSink(ms), options => options
                .WithCustomArgument("-ac 1 -ar 8000")
                .ForceFormat("f32le")
                .WithAudioCodec("pcm_f32le"))
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously(throwOnError: false);

        if (!success)
            return null;

        var buffer = ms.ToArray();
        if (buffer.Length < 4)
            return null;

        var sampleCount = buffer.Length / sizeof(float);
        var samples = new float[sampleCount];
        Buffer.BlockCopy(buffer, 0, samples, 0, sampleCount * sizeof(float));
        return samples;
    }

    private static float[] ComputePeaks(float[] samples, int peakCount)
    {
        var peaks = new float[peakCount];
        var samplesPerPeak = (double)samples.Length / peakCount;

        for (var i = 0; i < peakCount; i++)
        {
            var start = (int)(i * samplesPerPeak);
            var end = Math.Min((int)((i + 1) * samplesPerPeak), samples.Length);

            // RMS (Root Mean Square) — represents perceived loudness, not transient spikes
            double sumSquares = 0;
            var count = end - start;
            for (var j = start; j < end; j++)
                sumSquares += samples[j] * samples[j];

            peaks[i] = count > 0 ? MathF.Sqrt((float)(sumSquares / count)) : 0;
        }

        // Normalize to 0.0–1.0
        var globalMax = peaks.Max();
        if (globalMax > 0)
        {
            for (var i = 0; i < peaks.Length; i++)
                peaks[i] /= globalMax;
        }

        return peaks;
    }
}
