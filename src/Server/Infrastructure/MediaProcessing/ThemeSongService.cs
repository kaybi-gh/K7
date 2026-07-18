using System.Diagnostics;
using System.Globalization;
using FFMpegCore;
using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.MediaProcessing;

public sealed class ThemeSongService(
    IApplicationDbContext context,
    IOptions<PathsConfiguration> pathsOptions,
    ILogger<ThemeSongService> logger) : IThemeSongService
{
    private const double MaxThemeDurationSeconds = 90;
    private const double FadeSeconds = 1.5;
    private const int Mp3BitrateKbps = 96;

    private readonly PathsConfiguration _paths = pathsOptions.Value;

    public async Task<bool> HasThemeSongAsync(Guid mediaId, CancellationToken cancellationToken = default) =>
        await ResolvePlayablePathAsync(mediaId, cancellationToken) is not null;

    public async Task<string?> ResolvePlayablePathAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var media = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == mediaId)
            .Select(m => new { m.Id, m.Type })
            .FirstOrDefaultAsync(cancellationToken);

        if (media is null)
            return null;

        if (media.Type == MediaType.Movie)
            return await ResolveMovieThemePathAsync(mediaId, cancellationToken);

        if (media.Type == MediaType.Serie)
            return await ResolveSerieThemePathAsync(mediaId, cancellationToken);

        return null;
    }

    public async Task ExtractSerieThemeAsync(Guid serieId, CancellationToken cancellationToken = default)
    {
        var serieExists = await context.Medias
            .OfType<Serie>()
            .AsNoTracking()
            .AnyAsync(s => s.Id == serieId, cancellationToken);

        if (!serieExists)
        {
            logger.LogWarning("Theme extract skipped: serie {SerieId} not found", serieId);
            return;
        }

        if (await ResolveSerieLibrarySidecarAsync(serieId, cancellationToken) is not null)
        {
            logger.LogDebug("Theme extract skipped: library sidecar present for serie {SerieId}", serieId);
            return;
        }

        var themeGenerationAllowed = await (
            from episode in context.Medias.OfType<SerieEpisode>().AsNoTracking()
            where episode.SerieId == serieId
            join file in context.IndexedFiles.AsNoTracking() on episode.Id equals file.MediaId
            join library in context.Libraries.AsNoTracking() on file.LibraryId equals library.Id
            where library.IntroDetectionEnabled && library.ThemeSongGenerationEnabled
            select library.Id
        ).AnyAsync(cancellationToken);

        if (!themeGenerationAllowed)
        {
            logger.LogDebug(
                "Theme extract skipped: intro detection or theme generation disabled for serie {SerieId}",
                serieId);
            return;
        }

        var generatedPath = ThemeSongLocator.GetGeneratedPath(_paths, serieId);
        if (File.Exists(generatedPath))
        {
            logger.LogDebug("Theme extract skipped: generated theme already exists for serie {SerieId}", serieId);
            return;
        }

        var introSource = await (
            from episode in context.Medias.OfType<SerieEpisode>().AsNoTracking()
            where episode.SerieId == serieId
            join segment in context.MediaSegments.AsNoTracking()
                on episode.Id equals segment.MediaId
            where segment.Type == MediaSegmentType.Intro
            join file in context.IndexedFiles.AsNoTracking()
                on episode.Id equals file.MediaId
            where file.Path != null
            orderby episode.EpisodeNumber
            select new
            {
                FilePath = file.Path,
                segment.StartMs,
                segment.EndMs
            }
        ).FirstOrDefaultAsync(cancellationToken);

        if (introSource is null || string.IsNullOrEmpty(introSource.FilePath) || !File.Exists(introSource.FilePath))
        {
            logger.LogDebug("Theme extract skipped: no Intro segment with file for serie {SerieId}", serieId);
            return;
        }

        var startSeconds = introSource.StartMs / 1000.0;
        var durationSeconds = Math.Min(
            MaxThemeDurationSeconds,
            (introSource.EndMs - introSource.StartMs) / 1000.0);

        if (durationSeconds < 5)
        {
            logger.LogDebug("Theme extract skipped: intro too short ({Duration}s) for serie {SerieId}", durationSeconds, serieId);
            return;
        }

        var directory = Path.GetDirectoryName(generatedPath)!;
        Directory.CreateDirectory(directory);

        var fadeOutStart = Math.Max(0, durationSeconds - FadeSeconds);
        var filter = string.Create(
            CultureInfo.InvariantCulture,
            $"afade=t=in:st=0:d={FadeSeconds},afade=t=out:st={fadeOutStart}:d={FadeSeconds}");

        var args = string.Create(
            CultureInfo.InvariantCulture,
            $"-y -ss {startSeconds:F3} -t {durationSeconds:F3} -i \"{introSource.FilePath}\" -vn -af \"{filter}\" -c:a libmp3lame -b:a {Mp3BitrateKbps}k \"{generatedPath}\"");

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GlobalFFOptions.GetFFMpegBinaryPath(),
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                logger.LogWarning("Failed to start FFmpeg for theme extract on serie {SerieId}", serieId);
                return;
            }

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(180));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Theme extract timed out for serie {SerieId}", serieId);
                try { process.Kill(entireProcessTree: true); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                TryDelete(generatedPath);
                return;
            }

            var stderr = await stderrTask;
            if (process.ExitCode != 0 || !File.Exists(generatedPath))
            {
                logger.LogWarning(
                    "Theme extract failed for serie {SerieId} (exit {ExitCode}): {Stderr}",
                    serieId, process.ExitCode, stderr);
                TryDelete(generatedPath);
                return;
            }

            logger.LogInformation("Extracted theme song for serie {SerieId} to {Path}", serieId, generatedPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Theme extract error for serie {SerieId}", serieId);
            TryDelete(generatedPath);
        }
    }

    private async Task<string?> ResolveMovieThemePathAsync(Guid movieId, CancellationToken cancellationToken)
    {
        var filePath = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId == movieId && f.Path != null)
            .Select(f => f.Path)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(filePath))
            return null;

        var folder = Path.GetDirectoryName(filePath);
        return ThemeSongLocator.FindLibrarySidecar(folder, filePath);
    }

    private async Task<string?> ResolveSerieThemePathAsync(Guid serieId, CancellationToken cancellationToken)
    {
        var sidecar = await ResolveSerieLibrarySidecarAsync(serieId, cancellationToken);
        if (sidecar is not null)
            return sidecar;

        var generated = ThemeSongLocator.GetGeneratedPath(_paths, serieId);
        return File.Exists(generated) ? generated : null;
    }

    private async Task<string?> ResolveSerieLibrarySidecarAsync(Guid serieId, CancellationToken cancellationToken)
    {
        var episodePath = await (
            from episode in context.Medias.OfType<SerieEpisode>().AsNoTracking()
            where episode.SerieId == serieId
            join file in context.IndexedFiles.AsNoTracking() on episode.Id equals file.MediaId
            where file.Path != null
            orderby episode.EpisodeNumber
            select file.Path
        ).FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(episodePath))
            return null;

        var serieRoot = ThemeSongLocator.ResolveSerieRootFromEpisodePath(episodePath);
        return ThemeSongLocator.FindLibrarySidecar(serieRoot);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
