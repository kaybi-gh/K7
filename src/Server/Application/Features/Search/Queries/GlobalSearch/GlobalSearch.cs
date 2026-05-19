using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
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

public class GlobalSearchQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GlobalSearchQuery, GlobalSearchResultDto>
{
    public async Task<GlobalSearchResultDto> Handle(GlobalSearchQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.Q);

        if (request.Q.Trim().Length < 2)
            return new GlobalSearchResultDto();

        var term = $"%{request.Q.Trim().ToLower()}%";
        var limit = Math.Min(request.PageSize, 50);

        var mediaQuery = context.Medias
            .Include(m => m.Pictures)
                .ThenInclude(p => p.Variants)
            .Where(m => EF.Functions.Like(m.Title!.ToLower(), term))
            .OrderBy(m => EF.Functions.Like(m.Title!.ToLower(), request.Q.Trim().ToLower()) ? 0 : 1)
            .Take(limit)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Studio))
        {
            var studio = request.Studio.Trim();
            mediaQuery = context.Medias
                .Include(m => m.Pictures)
                    .ThenInclude(p => p.Variants)
                .Where(m => (m is Movie && ((Movie)m).Studios.Contains(studio))
                         || (m is Serie && ((Serie)m).Studios.Contains(studio)))
                .Where(m => EF.Functions.Like(m.Title!.ToLower(), term))
                .OrderBy(m => m.Title)
                .Take(limit)
                .AsNoTracking();
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
}
