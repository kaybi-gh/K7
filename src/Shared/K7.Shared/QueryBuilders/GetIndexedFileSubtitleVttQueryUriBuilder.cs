namespace K7.Shared.QueryBuilders;

public static class GetIndexedFileSubtitleVttQueryUriBuilder
{
    public const string Route = "/api/indexed-files/{id}/subtitles/{subtitleTrackIndex}.vtt";

    public static string Build(Guid id, int subtitleTrackIndex) => Route
        .Replace("{id}", $"{id}")
        .Replace("{subtitleTrackIndex}", $"{subtitleTrackIndex}");
}
