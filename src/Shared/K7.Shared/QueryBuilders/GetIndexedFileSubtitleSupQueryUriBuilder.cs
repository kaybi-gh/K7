using System.Globalization;

namespace K7.Shared.QueryBuilders;

/// <summary>
/// Windowed lossless PGS (.sup) sidecar of an embedded bitmap subtitle track.
/// </summary>
public static class GetIndexedFileSubtitleSupQueryUriBuilder
{
    public const string Route = "/api/indexed-files/{id}/subtitles/{subtitleTrackIndex}.sup";

    public static string Build(Guid id, int subtitleTrackIndex, double positionSeconds, double durationSeconds)
    {
        var path = Route
            .Replace("{id}", $"{id}")
            .Replace("{subtitleTrackIndex}", $"{subtitleTrackIndex}");
        var position = Math.Max(0, positionSeconds).ToString("0.###", CultureInfo.InvariantCulture);
        var duration = Math.Max(0, durationSeconds).ToString("0.###", CultureInfo.InvariantCulture);
        return $"{path}?position={position}&duration={duration}";
    }
}
