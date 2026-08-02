using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Persons.Commands.RefreshPersonMetadata;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Common.Services;

public sealed class SerieSupplementalCastEnrichmentService(
    IApplicationDbContext context,
    ISender sender,
    ITvdbPersonLinkProvider tvdbPersonLinkProvider)
{
    private readonly IApplicationDbContext _context = context;
    private readonly ISender _sender = sender;
    private readonly ITvdbPersonLinkProvider _tvdbPersonLinkProvider = tvdbPersonLinkProvider;

    public async Task EnrichFromSupplementalAsync(
        Serie serie,
        IReadOnlyList<BasePersonRole> supplementalRoles,
        string language,
        CancellationToken cancellationToken = default)
    {
        var enrichment = SupplementalSerieCastEnricher.Enrich(serie.PersonRoles, supplementalRoles);

        foreach (var tvdbPeopleId in enrichment.UnresolvedTvdbPeopleIds)
        {
            var linkedIds = await _tvdbPersonLinkProvider.FetchLinkedExternalIdsAsync(tvdbPeopleId, cancellationToken);
            if (linkedIds.Count == 0)
                continue;

            foreach (var role in serie.PersonRoles.Where(r =>
                         r.Person.ExternalIds.Any(e => e.ProviderName == "tvdb" && e.Value == tvdbPeopleId)))
            {
                foreach (var linked in linkedIds)
                {
                    if (role.Person.ExternalIds.Any(e =>
                            string.Equals(e.ProviderName, linked.ProviderName, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    role.Person.ExternalIds.Add(new ExternalId
                    {
                        ProviderName = linked.ProviderName,
                        Value = linked.Value,
                        PersonId = role.Person.Id
                    });
                }
            }
        }

        if (enrichment.RolesToAppend.Count > 0)
        {
            await ResolvePersonReferencesAsync(enrichment.RolesToAppend, cancellationToken);
            foreach (var role in enrichment.RolesToAppend)
                serie.PersonRoles.Add(role);
        }

        await ResolvePersonReferencesAsync(serie.PersonRoles, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var person in serie.PersonRoles.Select(r => r.Person).Distinct())
        {
            if (person.Id == Guid.Empty || !PersonMetadataMergeHelper.NeedsProviderRefresh(person))
                continue;

            var tmdbId = person.ExternalIds.FirstOrDefault(e => e.ProviderName == "tmdb")?.Value;
            if (string.IsNullOrWhiteSpace(tmdbId))
                continue;

            await _sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new RefreshPersonMetadataCommand
                {
                    PersonId = person.Id,
                    ProviderName = "tmdb",
                    ProviderId = tmdbId,
                    Language = language
                },
                TargetEntityId = person.Id,
                TargetEntityTypeName = nameof(Person),
                Lane = BackgroundTaskLane.Metadata,
                MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName("tmdb"),
                WorkClass = BackgroundTaskWorkClass.Polish,
                TriggeredBy = BackgroundTaskTriggeredBy.System,
                MaxAttempts = 3
            }, cancellationToken);
        }
    }

    private async Task ResolvePersonReferencesAsync(IEnumerable<BasePersonRole> roles, CancellationToken cancellationToken)
    {
        foreach (var role in roles.ToList())
        {
            if (role.Person is null)
                continue;

            var matchedPersons = new List<Person>();
            foreach (var externalId in role.Person.ExternalIds.ToList())
            {
                var match = await _context.Persons
                    .Include(p => p.ExternalIds)
                    .Include(p => p.PortraitPicture)
                    .FirstOrDefaultAsync(p => p.ExternalIds.Any(e =>
                        e.ProviderName == externalId.ProviderName && e.Value == externalId.Value),
                        cancellationToken);
                if (match is not null && matchedPersons.All(p => p.Id != match.Id))
                    matchedPersons.Add(match);
            }

            Person? existingPerson = matchedPersons.Count > 0
                ? PickCanonicalPerson(matchedPersons)
                : await _context.Persons
                    .Include(p => p.ExternalIds)
                    .Include(p => p.PortraitPicture)
                    .FirstOrDefaultAsync(p => p.Name == role.Person.Name, cancellationToken);

            if (existingPerson is null)
                continue;

            foreach (var duplicate in matchedPersons.Where(p => !ReferenceEquals(p, existingPerson)))
                PersonMetadataMergeHelper.MergeMissingPersonData(existingPerson, duplicate);

            if (!ReferenceEquals(existingPerson, role.Person))
                PersonMetadataMergeHelper.MergeMissingPersonData(existingPerson, role.Person);

            role.Person = existingPerson;
        }
    }

    private static Person PickCanonicalPerson(IReadOnlyList<Person> persons)
    {
        return persons
            .OrderByDescending(p => p.ExternalIds.Any(e => e.ProviderName == "tmdb"))
            .ThenByDescending(p => !string.IsNullOrWhiteSpace(p.Biography))
            .ThenByDescending(p => p.Birthday.HasValue)
            .ThenBy(p => p.Id)
            .First();
    }
}
