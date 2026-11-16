using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.MediaShares.Services;

public sealed record ShareMediaDescription(MediaType Type, string Title, string? Subtitle, Guid? PictureId)
{
    public string? PictureUri => PictureId is { } id ? $"/api/metadata-pictures/{id}" : null;
}

/// <summary>
/// Display title, parent context and artwork of shared media, for share lists and the
/// public landing page. Episodes and seasons often have no title of their own.
/// </summary>
public static class ShareMediaDescriber
{
    private static readonly MetadataPictureType[] PosterFirst =
        [MetadataPictureType.Poster, MetadataPictureType.Cover, MetadataPictureType.Still];
    private static readonly MetadataPictureType[] CoverFirst =
        [MetadataPictureType.Cover, MetadataPictureType.Poster, MetadataPictureType.Portrait];
    private static readonly MetadataPictureType[] StillFirst =
        [MetadataPictureType.Still, MetadataPictureType.Poster, MetadataPictureType.Cover];

    public static async Task<IReadOnlyDictionary<Guid, ShareMediaDescription>> DescribeAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, ShareMediaDescription>();
        if (mediaIds.Count == 0)
            return result;

        var medias = await context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Type, m.Title })
            .ToListAsync(cancellationToken);

        var episodes = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Where(e => mediaIds.Contains(e.Id))
            .Select(e => new { e.Id, e.SerieId, SerieTitle = e.Serie.Title, e.Season.SeasonNumber, e.EpisodeNumber })
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var seasons = await context.Medias
            .OfType<SerieSeason>()
            .AsNoTracking()
            .Where(s => mediaIds.Contains(s.Id))
            .Select(s => new { s.Id, s.SerieId, SerieTitle = s.Serie.Title, s.SeasonNumber })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var tracks = await context.Medias
            .OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => mediaIds.Contains(t.Id))
            .Select(t => new { t.Id, t.AlbumId, AlbumTitle = t.Album.Title })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var albums = await context.Medias
            .OfType<MusicAlbum>()
            .AsNoTracking()
            .Where(a => mediaIds.Contains(a.Id))
            .Select(a => new { a.Id, ArtistName = a.Artist != null ? a.Artist.Title : null })
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // Parents lend their artwork when the shared media has none.
        var parentIds = new Dictionary<Guid, Guid>();
        foreach (var episode in episodes.Values)
            parentIds[episode.Id] = episode.SerieId;
        foreach (var season in seasons.Values)
            parentIds[season.Id] = season.SerieId;
        foreach (var track in tracks.Values)
            parentIds[track.Id] = track.AlbumId;

        var pictureOwnerIds = mediaIds.Concat(parentIds.Values).Distinct().ToList();
        var pictures = (await context.MetadataPictures
                .AsNoTracking()
                .Where(p => p.MediaId != null && pictureOwnerIds.Contains(p.MediaId.Value) && p.LocalPath != null)
                .Select(p => new { MediaId = p.MediaId!.Value, p.Id, p.Type })
                .ToListAsync(cancellationToken))
            .ToLookup(p => p.MediaId, p => (p.Id, p.Type));

        foreach (var media in medias)
        {
            var title = media.Title;
            string? subtitle = null;

            if (episodes.TryGetValue(media.Id, out var episode))
            {
                var numbering = $"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00}";
                subtitle = string.IsNullOrWhiteSpace(episode.SerieTitle)
                    ? numbering
                    : $"{episode.SerieTitle} - {numbering}";
                title = string.IsNullOrWhiteSpace(title) ? numbering : title;
            }
            else if (seasons.TryGetValue(media.Id, out var season))
            {
                subtitle = season.SerieTitle;
                title = string.IsNullOrWhiteSpace(title) ? $"S{season.SeasonNumber:00}" : title;
            }
            else if (tracks.TryGetValue(media.Id, out var track))
            {
                subtitle = track.AlbumTitle;
            }
            else if (albums.TryGetValue(media.Id, out var album))
            {
                subtitle = album.ArtistName;
            }

            var preferred = media.Type switch
            {
                MediaType.MusicAlbum or MediaType.MusicArtist or MediaType.MusicTrack => CoverFirst,
                MediaType.SerieEpisode => StillFirst,
                _ => PosterFirst
            };

            var pictureId = Pick(pictures[media.Id], preferred);
            if (pictureId is null && parentIds.TryGetValue(media.Id, out var parentId))
                pictureId = Pick(pictures[parentId], preferred);

            result[media.Id] = new ShareMediaDescription(
                media.Type,
                string.IsNullOrWhiteSpace(title) ? media.Type.ToString() : title,
                subtitle,
                pictureId);
        }

        return result;
    }

    private static Guid? Pick(IEnumerable<(Guid Id, MetadataPictureType Type)> pictures, MetadataPictureType[] preferred)
    {
        var list = pictures.ToList();
        foreach (var type in preferred)
        {
            foreach (var picture in list)
            {
                if (picture.Type == type)
                    return picture.Id;
            }
        }

        return list.Count > 0 ? list[0].Id : null;
    }
}
