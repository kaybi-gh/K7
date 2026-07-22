using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Interfaces;

/// <summary>
/// Resolves cross-provider external ids for a TheTVDB people record (e.g. tmdb, imdb).
/// </summary>
public interface ITvdbPersonLinkProvider
{
    Task<IReadOnlyList<ExternalIdEntry>> FetchLinkedExternalIdsAsync(
        string tvdbPeopleId,
        CancellationToken cancellationToken = default);
}
