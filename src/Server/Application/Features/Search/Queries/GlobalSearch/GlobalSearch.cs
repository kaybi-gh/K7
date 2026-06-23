using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Search;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Search.Queries.GlobalSearch;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GlobalSearchQuery : IRequest<GlobalSearchResultDto>
{
    public required string Q { get; init; }
    public string? Studio { get; init; }
    public int PageSize { get; init; } = 10;
}

public class GlobalSearchQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GlobalSearchQuery, GlobalSearchResultDto>
{
    public async Task<GlobalSearchResultDto> Handle(GlobalSearchQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.Q);

        if (request.Q.Trim().Length < 2)
            return new GlobalSearchResultDto();

        var term = $"%{request.Q.Trim().ToLower()}%";
        var limit = Math.Min(request.PageSize, 50);

        var mediaQuery = ApplySearchMediaIncludes(context.Medias)
            .Where(m => EF.Functions.Like(m.Title!.ToLower(), term));

        if (!string.IsNullOrWhiteSpace(request.Studio))
        {
            var studio = request.Studio.Trim();
            mediaQuery = mediaQuery
                .Where(m => m.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Studio && mt.MetadataTag.DisplayName == studio))
                .OrderBy(m => m.Title);
        }
        else
        {
            mediaQuery = mediaQuery
                .OrderBy(m => EF.Functions.Like(m.Title!.ToLower(), request.Q.Trim().ToLower()) ? 0 : 1);
        }

        var personQuery = context.Persons
            .Include(p => p.PortraitPicture)
                .ThenInclude(pp => pp!.Variants)
            .Where(p => EF.Functions.Like(p.Name.ToLower(), term))
            .OrderBy(p => EF.Functions.Like(p.Name.ToLower(), request.Q.Trim().ToLower()) ? 0 : 1)
            .Take(limit)
            .AsNoTracking();

        var characterQuery = context.PersonRoles
            .OfType<Actor>()
            .Include(r => r.Person)
                .ThenInclude(p => p.PortraitPicture)
                    .ThenInclude(pp => pp!.Variants)
            .Include(r => r.Media)
            .Where(r => EF.Functions.Like(r.CharacterName.ToLower(), term))
            .Take(limit)
            .AsNoTracking();

        var voiceActorQuery = context.PersonRoles
            .OfType<VoiceActor>()
            .Include(r => r.Person)
                .ThenInclude(p => p.PortraitPicture)
                    .ThenInclude(pp => pp!.Variants)
            .Include(r => r.Media)
            .Where(r => EF.Functions.Like(r.CharacterName.ToLower(), term))
            .Take(limit)
            .AsNoTracking();

        if (currentUser.Id is { } userId)
        {
            var restrictionProfile = await context.ContentRestrictionProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Users.Any(u => u.Id == userId), cancellationToken);

            mediaQuery = ApplyUserExclusions(mediaQuery, userId);
            if (restrictionProfile is not null)
                mediaQuery = ContentRestrictionEvaluator.ApplyRestriction(mediaQuery, restrictionProfile);

            var accessibleMediaIds = ApplyUserExclusions(context.Medias, userId);
            if (restrictionProfile is not null)
                accessibleMediaIds = ContentRestrictionEvaluator.ApplyRestriction(accessibleMediaIds, restrictionProfile);

            var accessibleMediaIdQuery = accessibleMediaIds.Select(m => m.Id);
            characterQuery = characterQuery.Where(r => accessibleMediaIdQuery.Contains(r.MediaId));
            voiceActorQuery = voiceActorQuery.Where(r => accessibleMediaIdQuery.Contains(r.MediaId));
        }

        mediaQuery = mediaQuery.Take(limit).AsNoTracking();

        var (medias, persons, actors, voiceActors) = (
            await mediaQuery.ToListAsync(cancellationToken),
            await personQuery.ToListAsync(cancellationToken),
            await characterQuery.ToListAsync(cancellationToken),
            await voiceActorQuery.ToListAsync(cancellationToken)
        );

        var characterResults = actors
            .Select(r => new CharacterSearchResultDto
            {
                PersonRoleId = r.Id,
                CharacterName = r.CharacterName,
                PersonId = r.PersonId,
                PersonName = r.Person.Name,
                PersonPortrait = r.Person.PortraitPicture?.ToMetadataPictureDto(),
                MediaId = r.MediaId,
                MediaTitle = r.Media.Title,
                MediaType = r.Media.Type
            })
            .Concat(voiceActors.Select(r => new CharacterSearchResultDto
            {
                PersonRoleId = r.Id,
                CharacterName = r.CharacterName,
                PersonId = r.PersonId,
                PersonName = r.Person.Name,
                PersonPortrait = r.Person.PortraitPicture?.ToMetadataPictureDto(),
                MediaId = r.MediaId,
                MediaTitle = r.Media.Title,
                MediaType = r.Media.Type
            }))
            .DistinctBy(r => r.PersonRoleId)
            .ToList();

        return new GlobalSearchResultDto
        {
            MediaResults = medias.Select(m => m.ToLiteMediaDto()).ToList(),
            PersonResults = persons.Select(p => p.ToLitePersonDto()).ToList(),
            CharacterResults = characterResults
        };
    }

    private IQueryable<BaseMedia> ApplyUserExclusions(IQueryable<BaseMedia> query, Guid userId)
    {
        var excludedLibraryIds = context.UserLibraryExclusions
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.LibraryId);

        query = query.Where(x =>
            x is MusicAlbum
                ? x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                    || ((MusicAlbum)x).Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                        || t.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)))
                : x is MusicArtist
                    ? ((MusicArtist)x).Albums.Any(a => a.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                        || a.Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                            || t.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))))
                : x is MusicTrack
                    ? x.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                        || x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                        || ((MusicTrack)x).Album.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                        || ((MusicTrack)x).Album.Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId)))
                : x is Serie
                    ? x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                        || ((Serie)x).Seasons.Any(s => s.Episodes.Any(e => e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                            || e.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))))
                : x is SerieSeason
                    ? ((SerieSeason)x).Episodes.Any(e => e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                        || e.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)))
                    : x.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                        || x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)));

        var excludedMediaIds = context.UserMediaExclusions
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.MediaId);

        return query.WhereNotUserExcluded(excludedMediaIds);
    }

    private static IQueryable<BaseMedia> ApplySearchMediaIncludes(IQueryable<BaseMedia> query) =>
        query
            .IncludeMetadataTagsForMapping()
            .Include(m => m.Pictures)
                .ThenInclude(p => p.Variants)
            .Include(m => ((MusicTrack)m).Album)
                .ThenInclude(a => a!.Pictures)
                    .ThenInclude(p => p!.Variants)
            .Include(m => ((SerieEpisode)m).Season)
                .ThenInclude(s => s!.Pictures)
                    .ThenInclude(p => p!.Variants)
            .Include(m => ((SerieEpisode)m).Serie)
                .ThenInclude(s => s!.Pictures)
                    .ThenInclude(p => p!.Variants)
            .Include(m => ((SerieEpisode)m).Serie)
                .ThenInclude(s => s!.Seasons);
}
