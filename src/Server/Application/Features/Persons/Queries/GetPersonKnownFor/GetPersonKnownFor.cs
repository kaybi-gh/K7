using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Interfaces;
using K7.Shared.Dtos.Entities.Persons;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Persons.Queries.GetPersonKnownFor;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record GetPersonKnownForQuery : IRequest<List<PersonKnownForItemDto>>
{
    public required Guid PersonId { get; init; }
    public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
}

public class GetPersonKnownForQueryHandler(
    IApplicationDbContext context,
    IPersonCreditsProvider creditsProvider,
    IUser currentUser,
    MediaAccessFilter mediaAccessFilter)
    : IRequestHandler<GetPersonKnownForQuery, List<PersonKnownForItemDto>>
{
    public async Task<List<PersonKnownForItemDto>> Handle(
        GetPersonKnownForQuery request, CancellationToken cancellationToken)
    {
        if (await HasActiveRestrictionProfileAsync(cancellationToken))
            return [];

        var person = await context.Persons
            .AsNoTracking()
            .Include(p => p.ExternalIds)
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);

        if (person is null)
            return [];

        var tmdbId = person.ExternalIds
            .FirstOrDefault(e => e.ProviderName == MetadataProviderNames.Tmdb)?.Value;

        if (tmdbId is null)
            return [];

        var allCredits = await creditsProvider.GetPersonCreditsAsync(tmdbId, cancellationToken);
        if (allCredits.Count == 0)
            return [];

        // Filter out media already in K7 library
        var externalIds = allCredits.Select(c => c.ExternalId).ToList();
        var localIdSet = new HashSet<string>();
        foreach (var externalIdBatch in externalIds.Distinct().Chunk(500))
        {
            var localExternalIds = await context.Medias
                .AsNoTracking()
                .Where(m => m.ExternalIds.Any(e => e.ProviderName == MetadataProviderNames.Tmdb && externalIdBatch.Contains(e.Value)))
                .SelectMany(m => m.ExternalIds)
                .Where(e => e.ProviderName == MetadataProviderNames.Tmdb)
                .Select(e => e.Value)
                .ToListAsync(cancellationToken);

            localIdSet.UnionWith(localExternalIds);
        }

        return allCredits
            .Where(c => !localIdSet.Contains(c.ExternalId))
            .DistinctBy(c => (c.ExternalId, c.MediaType))
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

    private async Task<bool> HasActiveRestrictionProfileAsync(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return false;

        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        var restrictionProfile = await mediaAccessFilter.GetRestrictionProfileAsync(
            userId, sharedProfileId, cancellationToken);
        return restrictionProfile is not null;
    }
}
