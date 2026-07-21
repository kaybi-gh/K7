using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Services;

public static class EpisodePictureResolver
{
    /// <summary>
    /// Card display pictures: season poster, then serie poster, then episode pictures.
    /// </summary>
    public static IList<MetadataPicture>? ResolveDisplayPictures(SerieEpisode episode)
    {
        var merged = new List<MetadataPicture>();
        var seen = new HashSet<Guid>();

        AppendPictures(merged, seen, episode.Season?.Pictures);
        AppendPictures(merged, seen, episode.Serie?.Pictures);
        AppendPictures(merged, seen, episode.Pictures);

        return merged.Count > 0 ? merged : null;
    }

    /// <summary>
    /// Home-feed pictures: display posters first, then hero stills/backdrops for TV backdrop.
    /// </summary>
    public static IList<MetadataPicture>? MergeHeroAndDisplayPictures(SerieEpisode episode)
    {
        var hero = ResolveHeroPictures(episode);
        var display = ResolveDisplayPictures(episode);

        if (hero is null || hero.Count == 0)
            return display;
        if (display is null || display.Count == 0)
            return hero;

        var merged = new List<MetadataPicture>(hero.Count + display.Count);
        var seen = new HashSet<Guid>();
        foreach (var picture in display.Concat(hero))
        {
            if (seen.Add(picture.Id))
                merged.Add(picture);
        }

        return merged;
    }

    /// <summary>
    /// Hero/backdrop pictures: prefer episode still when available, then fall back to display pictures.
    /// </summary>
    public static IList<MetadataPicture>? ResolveHeroPictures(SerieEpisode episode)
    {
        if (episode.Pictures is { Count: > 0 } episodePictures
            && episodePictures.Any(p => p.Type == MetadataPictureType.Still))
        {
            return episodePictures;
        }

        return ResolveDisplayPictures(episode);
    }

    private static void AppendPictures(
        List<MetadataPicture> merged,
        HashSet<Guid> seen,
        IList<MetadataPicture>? pictures)
    {
        if (pictures is not { Count: > 0 })
            return;

        foreach (var picture in pictures)
        {
            if (seen.Add(picture.Id))
                merged.Add(picture);
        }
    }
}
