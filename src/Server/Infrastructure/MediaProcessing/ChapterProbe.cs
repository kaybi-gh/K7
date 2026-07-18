using System.Globalization;
using System.Text.Json;
using FFMpegCore;
using K7.Server.Domain.Entities.Metadatas.Files;

namespace K7.Server.Infrastructure.MediaProcessing;

public static class ChapterProbe
{
    public static async Task<List<ChapterMarker>> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var stdoutLines = new List<string>();
        var ffprobePath = GlobalFFOptions.GetFFProbeBinaryPath();

        var exitCode = await SafeProcessRunner.RunAsync(
            ffprobePath,
            $"-v quiet -print_format json -show_chapters \"{filePath}\"",
            onStdout: line => stdoutLines.Add(line),
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        if (exitCode != 0)
            return [];

        var json = string.Join("", stdoutLines);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("chapters", out var chapters))
            return [];

        var results = new List<ChapterMarker>();
        foreach (var chapter in chapters.EnumerateArray())
        {
            if (!chapter.TryGetProperty("start_time", out var startTime) ||
                !double.TryParse(startTime.GetString(), CultureInfo.InvariantCulture, out var startSeconds))
            {
                continue;
            }

            double? endSeconds = null;
            if (chapter.TryGetProperty("end_time", out var endTime) &&
                double.TryParse(endTime.GetString(), CultureInfo.InvariantCulture, out var end))
            {
                endSeconds = end;
            }

            string? title = null;
            if (chapter.TryGetProperty("tags", out var tags) &&
                tags.TryGetProperty("title", out var titleElement))
            {
                title = titleElement.GetString();
            }

            results.Add(new ChapterMarker
            {
                StartSeconds = startSeconds,
                EndSeconds = endSeconds,
                Title = title
            });
        }

        return results.OrderBy(c => c.StartSeconds).ToList();
    }
}
