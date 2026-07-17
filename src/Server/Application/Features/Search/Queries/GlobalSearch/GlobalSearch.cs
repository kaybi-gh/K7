using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Services;
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

public class GlobalSearchQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    LiteMediaProjectionService liteMediaProjection,
    MediaAccessFilter mediaAccessFilter,
    IDatabaseCapabilities databaseCapabilities)
    : IRequestHandler<GlobalSearchQuery, GlobalSearchResultDto>
{
    private const int MovieLimit = 15;
    private const int EpisodeLimit = 15;
    private const int SerieSeasonLimit = 10;
    private const int MusicLimit = 10;
    private const int PersonLimit = 10;
    private const int CharacterLimit = 10;

    public async Task<GlobalSearchResultDto> Handle(GlobalSearchQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.Q);

        if (request.Q.Trim().Length < 2)
            return new GlobalSearchResultDto();

        var term = MediaTextSearchHelper.BuildTitlePattern(request.Q, databaseCapabilities.SupportsTrigramSearch);
        var rawQuery = request.Q.Trim().ToLower();
        Guid? userId = currentUser.Id;

        var mediaQuery = BuildMediaSearchQuery(term, rawQuery, request.Studio);
        IQueryable<Guid>? accessibleMediaIds = null;

        if (userId is { } currentUserId)
        {
            var restrictionProfile = await mediaAccessFilter.GetRestrictionProfileAsync(currentUserId, cancellationToken);

            mediaQuery = mediaAccessFilter.ApplyExclusions(mediaQuery, currentUserId);
            if (restrictionProfile is not null)
                mediaQuery = ContentRestrictionEvaluator.ApplyRestriction(mediaQuery, restrictionProfile);

            accessibleMediaIds = mediaAccessFilter.GetAccessibleMediaIds(currentUserId);
            if (restrictionProfile is not null)
                accessibleMediaIds = ContentRestrictionEvaluator.ApplyRestriction(
                    context.Medias.Where(m => accessibleMediaIds.Contains(m.Id)),
                    restrictionProfile)
                    .Select(m => m.Id);
        }

        mediaQuery = mediaQuery.AsNoTracking();

        var movieIds = await mediaQuery
            .Where(m => m.Type == MediaType.Movie)
            .Select(m => m.Id)
            .Take(MovieLimit)
            .ToListAsync(cancellationToken);

        var serieSeasonIds = await mediaQuery
            .Where(m => m.Type == MediaType.Serie || m.Type == MediaType.SerieSeason)
            .Select(m => m.Id)
            .Take(SerieSeasonLimit)
            .ToListAsync(cancellationToken);

        var episodeIds = await mediaQuery
            .Where(m => m.Type == MediaType.SerieEpisode)
            .Select(m => m.Id)
            .Take(EpisodeLimit)
            .ToListAsync(cancellationToken);

        var musicIds = await mediaQuery
            .Where(m => m.Type == MediaType.MusicArtist || m.Type == MediaType.MusicAlbum || m.Type == MediaType.MusicTrack)
            .Select(m => m.Id)
            .Take(MusicLimit)
            .ToListAsync(cancellationToken);

        var selectedIds = movieIds
            .Concat(serieSeasonIds)
            .Concat(episodeIds)
            .Concat(musicIds)
            .ToList();

        var medias = await liteMediaProjection.GetLiteMediaDtosAsync(selectedIds, userId, cancellationToken);

        var personQuery = context.Persons
            .Include(p => p.PortraitPicture)
            .Where(p => EF.Functions.Like(p.Name.ToLower(), term))
            .OrderBy(p => EF.Functions.Like(p.Name.ToLower(), rawQuery) ? 0 : 1)
            .Take(PersonLimit)
            .AsNoTracking();

        var characterQuery = context.PersonRoles
            .OfType<Actor>()
            .Include(r => r.Person)
                .ThenInclude(p => p.PortraitPicture)
            .Include(r => r.Media)
            .Where(r => EF.Functions.Like(r.CharacterName.ToLower(), term))
            .Take(CharacterLimit)
            .AsNoTracking();

        var voiceActorQuery = context.PersonRoles
            .OfType<VoiceActor>()
            .Include(r => r.Person)
                .ThenInclude(p => p.PortraitPicture)
            .Include(r => r.Media)
            .Where(r => EF.Functions.Like(r.CharacterName.ToLower(), term))
            .Take(CharacterLimit)
            .AsNoTracking();

        if (accessibleMediaIds is not null)
        {
            characterQuery = characterQuery.Where(r => accessibleMediaIds.Contains(r.MediaId));
            voiceActorQuery = voiceActorQuery.Where(r => accessibleMediaIds.Contains(r.MediaId));
        }

        var persons = await personQuery.ToListAsync(cancellationToken);
        var actors = await characterQuery.ToListAsync(cancellationToken);
        var voiceActors = await voiceActorQuery.ToListAsync(cancellationToken);

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
            MediaResults = medias,
            PersonResults = persons.Select(p => p.ToLitePersonDto()).ToList(),
            CharacterResults = characterResults
        };
    }

    private IQueryable<BaseMedia> BuildMediaSearchQuery(string term, string rawQuery, string? studio)
    {
        var mediaQuery = context.Medias
            .Where(m => EF.Functions.Like(m.Title!.ToLower(), term));

        if (!string.IsNullOrWhiteSpace(studio))
        {
            mediaQuery = mediaQuery
                .Where(m => m.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Studio && mt.MetadataTag.DisplayName == studio.Trim()))
                .OrderBy(m => m.SortTitle ?? m.Title);
        }
        else
        {
            mediaQuery = mediaQuery
                .OrderBy(m => EF.Functions.Like(m.Title!.ToLower(), rawQuery) ? 0 : 1);
        }

        return mediaQuery;
    }
}
