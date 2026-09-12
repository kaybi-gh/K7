using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Restrictions;

namespace K7.Server.Application.Features.Restrictions.Services;

public sealed record AgeRestrictionGate(DateOnly DateOfBirth, bool HideUnratedTitles);

public static class AgeRestrictionEvaluator
{
    public static IQueryable<BaseMedia> Apply(
        IQueryable<BaseMedia> query,
        DateOnly dateOfBirth,
        DateOnly today,
        bool hideUnrated = true)
    {
        var viewerAge = AgeCalculator.GetCompletedYears(dateOfBirth, today);
        var allowedKeys = ContentRatingAgeMap.GetAllowedNormalizedKeys(viewerAge).ToArray();

        // Seasons and episodes inherit the parent serie ContentRating. Home and explore
        // list those leaves then fold them into serie/season hats.
        // hideUnrated is folded in C# so the default path does not emit a NOT EXISTS.
        if (hideUnrated)
        {
            return query.Where(m =>
                (m.Type != MediaType.Movie
                    && m.Type != MediaType.Serie
                    && m.Type != MediaType.SerieSeason
                    && m.Type != MediaType.SerieEpisode)
                || ((m.Type == MediaType.Movie || m.Type == MediaType.Serie)
                    && m.MetadataTags.Any(mt =>
                        mt.MetadataTag.Kind == MetadataTagKind.ContentRating
                        && allowedKeys.Contains(mt.MetadataTag.NormalizedKey)))
                || (m.Type == MediaType.SerieSeason
                    && ((SerieSeason)m).Serie.MetadataTags.Any(mt =>
                        mt.MetadataTag.Kind == MetadataTagKind.ContentRating
                        && allowedKeys.Contains(mt.MetadataTag.NormalizedKey)))
                || (m.Type == MediaType.SerieEpisode
                    && ((SerieEpisode)m).Serie.MetadataTags.Any(mt =>
                        mt.MetadataTag.Kind == MetadataTagKind.ContentRating
                        && allowedKeys.Contains(mt.MetadataTag.NormalizedKey))));
        }

        return query.Where(m =>
            (m.Type != MediaType.Movie
                && m.Type != MediaType.Serie
                && m.Type != MediaType.SerieSeason
                && m.Type != MediaType.SerieEpisode)
            || ((m.Type == MediaType.Movie || m.Type == MediaType.Serie)
                && (m.MetadataTags.Any(mt =>
                        mt.MetadataTag.Kind == MetadataTagKind.ContentRating
                        && allowedKeys.Contains(mt.MetadataTag.NormalizedKey))
                    || !m.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.ContentRating)))
            || (m.Type == MediaType.SerieSeason && (
                ((SerieSeason)m).Serie.MetadataTags.Any(mt =>
                    mt.MetadataTag.Kind == MetadataTagKind.ContentRating
                    && allowedKeys.Contains(mt.MetadataTag.NormalizedKey))
                || !((SerieSeason)m).Serie.MetadataTags.Any(mt =>
                    mt.MetadataTag.Kind == MetadataTagKind.ContentRating)))
            || (m.Type == MediaType.SerieEpisode && (
                ((SerieEpisode)m).Serie.MetadataTags.Any(mt =>
                    mt.MetadataTag.Kind == MetadataTagKind.ContentRating
                    && allowedKeys.Contains(mt.MetadataTag.NormalizedKey))
                || !((SerieEpisode)m).Serie.MetadataTags.Any(mt =>
                    mt.MetadataTag.Kind == MetadataTagKind.ContentRating))));
    }

    public static bool IsActive(bool enabled, DateOnly? dateOfBirth) =>
        enabled && dateOfBirth is not null;
}
