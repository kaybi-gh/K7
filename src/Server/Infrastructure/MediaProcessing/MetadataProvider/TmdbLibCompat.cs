using K7.Server.Domain.Entities.Metadatas;
using TMDbLib.Objects.General;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

/// <summary>
/// Adapters for TMDbLib 3.0 nullable annotations and episode-number widening.
/// </summary>
internal static class TmdbLibCompat
{
    public static int ToEpisodeNumber(long episodeNumber)
    {
        if (episodeNumber is < int.MinValue or > int.MaxValue)
            throw new InvalidOperationException($"Episode number {episodeNumber} is outside the supported range.");

        return (int)episodeNumber;
    }

    public static IEnumerable<string> NonEmptyNames(IEnumerable<string?>? values)
    {
        if (values is null)
            yield break;

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    public static List<TrailerInfo> MapYoutubeTrailers(IEnumerable<Video>? videos)
    {
        if (videos is null)
            return [];

        var trailers = new List<TrailerInfo>();
        foreach (var video in videos)
        {
            if (video.Site != "YouTube" || video.Type is not ("Trailer" or "Teaser"))
                continue;

            if (video.Key is null || video.Name is null || video.Site is null || video.Type is null)
                continue;

            trailers.Add(new TrailerInfo
            {
                Key = video.Key,
                Name = video.Name,
                Site = video.Site,
                Type = video.Type,
                Language = video.Iso_639_1
            });
        }

        return trailers;
    }
}
