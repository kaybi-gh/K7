using System.Globalization;

namespace K7.Shared.QueryBuilders;

public static class GetIndexedFileSubtitleVttQueryUriBuilder
{
    public const string Route = "/api/indexed-files/{id}/subtitles/{subtitleTrackIndex}.vtt";

    /// <summary>
    /// Optional playback position in seconds. With it, the server may answer with the cues
    /// around that position while the whole track is still being extracted.
    /// </summary>
    public const string PositionQueryParameter = "position";

    public static string Build(Guid id, int subtitleTrackIndex, double? positionSeconds = null)
    {
        var uri = Route
            .Replace("{id}", $"{id}")
            .Replace("{subtitleTrackIndex}", $"{subtitleTrackIndex}");

        if (positionSeconds is double position && double.IsFinite(position) && position >= 0)
            uri += $"?{PositionQueryParameter}={position.ToString("0.#", CultureInfo.InvariantCulture)}";

        return uri;
    }
}
