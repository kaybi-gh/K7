using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Builds SeekBar chapter markers from embedded file chapters and K7 intro/outro segments,
/// with overlap deduplication when both sources describe the same region.
/// </summary>
public static class SeekBarChapterBuilder
{
    public const double DedupToleranceSeconds = 2.0;

    public sealed record Marker(string? Title, double StartSeconds);

    public static IReadOnlyList<Marker> Build(
        bool showChapterTicks,
        IReadOnlyList<ChapterMarkerDto>? fileChapters,
        IReadOnlyList<MediaSegmentDto>? segments,
        string introTitle,
        string outroTitle)
    {
        if (!showChapterTicks)
            return [];

        var markers = new List<Marker>();

        if (fileChapters is not null)
        {
            foreach (var chapter in fileChapters.OrderBy(c => c.StartSeconds))
                markers.Add(new Marker(chapter.Title, chapter.StartSeconds));
        }

        if (segments is not null)
        {
            foreach (var segment in segments)
            {
                if (segment.Type is not (K7.Shared.Enums.MediaSegmentType.Intro or K7.Shared.Enums.MediaSegmentType.Outro))
                    continue;

                var start = segment.StartMs / 1000.0;
                var end = segment.EndMs / 1000.0;
                if (end <= start)
                    continue;

                if (OverlapsExistingMarker(markers, start, end))
                    continue;

                var title = segment.Type == K7.Shared.Enums.MediaSegmentType.Intro ? introTitle : outroTitle;

                if (!HasNearbyMarker(markers, start))
                    markers.Add(new Marker(title, start));

                // End boundary so the SeekBar shows a gap after intro/outro (e.g. cold open after OP,
                // or a few seconds of content after ED) instead of stretching one segment to Duration.
                if (!HasNearbyMarker(markers, end))
                    markers.Add(new Marker(null, end));
            }
        }

        return FinalizeMarkers(markers);
    }

    private static bool OverlapsExistingMarker(List<Marker> markers, double start, double end) =>
        markers.Any(m =>
            m.StartSeconds >= start - DedupToleranceSeconds
            && m.StartSeconds <= end + DedupToleranceSeconds);

    private static bool HasNearbyMarker(List<Marker> markers, double time) =>
        markers.Any(m => Math.Abs(m.StartSeconds - time) <= DedupToleranceSeconds);

    /// <summary>
    /// SeekBar only draws from the first marker start; prepend 0 when the first tick is later
    /// (e.g. anime cold open before OP, or outro-only markers).
    /// </summary>
    private static IReadOnlyList<Marker> FinalizeMarkers(List<Marker> markers)
    {
        var sorted = markers
            .OrderBy(m => m.StartSeconds)
            .ThenBy(m => m.Title is null ? 1 : 0)
            .ToList();

        if (sorted.Count > 0 && sorted[0].StartSeconds > DedupToleranceSeconds)
            sorted.Insert(0, new Marker(null, 0));

        return sorted;
    }
}
