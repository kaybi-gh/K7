using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Interfaces;
using K7.Shared.Dtos.Entities.Persons;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Persons.Queries.GetPersonKnownFor;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record GetPersonKnownForQuery : IRequest<List<PersonKnownForItemDto>>
{
    public required Guid PersonId { get; init; }
    public int PageSize { get; init; } = 20;
}

public class GetPersonKnownForQueryHandler(
    IApplicationDbContext context,
    IPersonCreditsProvider creditsProvider)
    : IRequestHandler<GetPersonKnownForQuery, List<PersonKnownForItemDto>>
{
    public async Task<List<PersonKnownForItemDto>> Handle(
        GetPersonKnownForQuery request, CancellationToken cancellationToken)
    {
        var person = await context.Persons
            .AsNoTracking()
            .Include(p => p.ExternalIds)
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);

        if (person is null)
            return [];

        var tmdbId = person.ExternalIds
            .FirstOrDefault(e => e.ProviderName == "tmdb")?.Value;

        if (tmdbId is null)
            return [];

        var allCredits = await creditsProvider.GetPersonCreditsAsync(tmdbId, cancellationToken);
        if (allCredits.Count == 0)
            return [];

        // Filter out media already in K7 library
        var externalIds = allCredits.Select(c => c.ExternalId).ToList();
        var localExternalIds = await context.Medias
            .AsNoTracking()
            .Where(m => m.ExternalIds.Any(e => e.ProviderName == "tmdb" && externalIds.Contains(e.Value)))
            .SelectMany(m => m.ExternalIds)
            .Where(e => e.ProviderName == "tmdb")
            .Select(e => e.Value)
            .ToListAsync(cancellationToken);

        var localIdSet = localExternalIds.ToHashSet();

        return allCredits
            .Where(c => !localIdSet.Contains(c.ExternalId))
            .Take(request.PageSize)
            .Select(c => new PersonKnownForItemDto
            {
                ExternalId = c.ExternalId,
                Title = c.Title,
                Year = c.Year,
                MediaType = c.MediaType,
                PosterUrl = c.PosterPath
            })
            .ToList();
    }
}
