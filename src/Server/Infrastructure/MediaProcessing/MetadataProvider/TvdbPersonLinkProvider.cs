using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

public sealed class TvdbPersonLinkProvider : ITvdbPersonLinkProvider
{
    private readonly TvdbApiClient _apiClient;
    private readonly ILogger<TvdbPersonLinkProvider> _logger;

    public TvdbPersonLinkProvider(TvdbApiClient apiClient, ILogger<TvdbPersonLinkProvider> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExternalIdEntry>> FetchLinkedExternalIdsAsync(
        string tvdbPeopleId,
        CancellationToken cancellationToken = default)
    {
        if (!_apiClient.IsConfigured || !int.TryParse(tvdbPeopleId, out var peopleId) || peopleId <= 0)
            return [];

        try
        {
            var people = await _apiClient.GetPeopleExtendedAsync(peopleId, cancellationToken);
            if (people?.RemoteIds is null || people.RemoteIds.Count == 0)
                return [];

            var results = new List<ExternalIdEntry>();
            foreach (var remote in people.RemoteIds)
            {
                if (string.IsNullOrWhiteSpace(remote.Id) || string.IsNullOrWhiteSpace(remote.SourceName))
                    continue;

                var providerName = TvdbExternalIdMapper.MapRemoteSourceToProviderName(remote.SourceName);
                if (providerName is null || providerName == "tvdb")
                    continue;

                if (results.Any(r => r.ProviderName == providerName))
                    continue;

                results.Add(new ExternalIdEntry(providerName, remote.Id.Trim()));
            }

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "TVDB people link lookup failed for {TvdbPeopleId}", tvdbPeopleId);
            return [];
        }
    }
}
