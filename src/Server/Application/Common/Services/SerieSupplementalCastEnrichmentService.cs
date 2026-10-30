using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
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
            await PersonRolePersonResolveHelper.ResolveAsync(
                _context, enrichment.RolesToAppend, cancellationToken);
            PersonRolePersonResolveHelper.PreferPersonLevelPortraits(enrichment.RolesToAppend);
            foreach (var role in enrichment.RolesToAppend)
                serie.PersonRoles.Add(role);
        }

        await PersonRolePersonResolveHelper.ResolveAsync(_context, serie.PersonRoles, cancellationToken);
        PersonRolePersonResolveHelper.PreferPersonLevelPortraits(serie.PersonRoles);

        await _context.SaveChangesAsync(cancellationToken);

        await PersonMetadataRefreshQueueHelper.QueueMissingTmdbDetailsAsync(
            _sender,
            serie.PersonRoles.Select(r => r.Person),
            language,
            cancellationToken);
    }

    public async Task ApplyEpisodePersonRolesAsync(
        SerieEpisode episode,
        IReadOnlyList<BasePersonRole> roles,
        CancellationToken cancellationToken = default)
    {
        if (episode.IsFieldLocked(nameof(SerieEpisode.PersonRoles)) || roles.Count == 0)
            return;

        await PersonRolePersonResolveHelper.ResolveAsync(_context, roles, cancellationToken);
        PersonRolePersonResolveHelper.PreferPersonLevelPortraits(roles);
        PersonRolePortraitReuseHelper.PreserveDownloadedPortraits(episode.PersonRoles, roles);
        episode.PersonRoles.Clear();
        foreach (var role in roles)
            episode.PersonRoles.Add(role);
    }

}
