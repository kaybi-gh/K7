using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Services;

/// <summary>
/// Adds the details media cards can show (genre, content rating, runtime, rating) to one page of
/// lite medias. Only called when the client asks for them: the default browse query stays as it
/// is. Three queries for the whole page, each by media id.
/// </summary>
public class LiteMediaCardDetailsService(IApplicationDbContext context)
{
    public async Task<List<LiteMediaDto>> ApplyAsync(
        IReadOnlyList<LiteMediaDto> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return [];

        var ids = items.Select(i => i.Id).ToList();

        var tags = await context.MediaMetadataTags
            .AsNoTracking()
            .Where(mt => ids.Contains(mt.MediaId)
                && (mt.MetadataTag.Kind == MetadataTagKind.Genre || mt.MetadataTag.Kind == MetadataTagKind.ContentRating))
            .Select(mt => new { mt.MediaId, mt.MetadataTag.Kind, mt.MetadataTag.DisplayName })
            .ToListAsync(cancellationToken);
        var tagsByMedia = tags.ToLookup(t => t.MediaId);

        var ratings = await context.Ratings
            .AsNoTracking()
            .OfType<MetadataProviderRating>()
            .Where(r => ids.Contains(r.MediaId) && r.MaximumValue > 0)
            .Select(r => new { r.MediaId, r.Value, r.MaximumValue })
            .ToListAsync(cancellationToken);
        var ratingByMedia = ratings
            .GroupBy(r => r.MediaId)
            .ToDictionary(g => g.Key, g => Math.Round(g.First().Value / g.First().MaximumValue * 10, 1));

        var runtimes = await HomeFeedRuntimeResolver.ResolveMinutesAsync(
            context,
            items.Select(i => (i.Id, TypeOf(i))).ToList(),
            cancellationToken);

        return items.Select(item =>
        {
            var mediaTags = tagsByMedia[item.Id].ToList();
            var details = new LiteMediaCardDetailsDto
            {
                Genre = mediaTags.FirstOrDefault(t => t.Kind == MetadataTagKind.Genre)?.DisplayName,
                ContentRating = mediaTags.FirstOrDefault(t => t.Kind == MetadataTagKind.ContentRating)?.DisplayName,
                RuntimeMinutes = runtimes.TryGetValue(item.Id, out var minutes) ? minutes : null,
                Rating = ratingByMedia.TryGetValue(item.Id, out var rating) ? rating : null
            };
            return details.IsEmpty ? item : item with { Details = details };
        }).ToList();
    }

    private static MediaType TypeOf(LiteMediaDto item) => item switch
    {
        LiteMovieDto => MediaType.Movie,
        LiteSerieDto => MediaType.Serie,
        LiteSerieSeasonDto => MediaType.SerieSeason,
        LiteSerieEpisodeDto => MediaType.SerieEpisode,
        LiteMusicAlbumDto => MediaType.MusicAlbum,
        LiteMusicTrackDto => MediaType.MusicTrack,
        _ => MediaType.MusicArtist
    };
}
