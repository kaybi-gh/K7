using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Playable = has at least one local or remote indexed file.
/// Used for navigation counts and list filtering (history deep links still resolve via GetMedia).
/// </summary>
public static class SeriePlayableHelper
{
    public static bool HasPlayableFile(SerieEpisode episode) =>
        episode.IndexedFiles.Count > 0 || episode.RemoteIndexedFiles.Count > 0;

    public static int CountPlayableEpisodes(IEnumerable<SerieEpisode> episodes) =>
        episodes.Count(HasPlayableFile);
}
