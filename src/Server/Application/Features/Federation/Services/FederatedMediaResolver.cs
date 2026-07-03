using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Services;

public class FederatedMediaResolver(IApplicationDbContext context) : IFederatedMediaResolver
{
    private static readonly string[] ProviderPriority = ["tmdb", "imdb", "tvdb", "musicbrainz", "isrc", "spotify"];

    public async Task<FederatedMediaResolutionResult> ResolveAsync(
        Guid peerServerId,
        FederatedMediaRef mediaRef,
        CancellationToken cancellationToken = default)
    {
        var results = await ResolveManyAsync(peerServerId, [mediaRef], cancellationToken);
        return results[mediaRef];
    }

    public async Task<IReadOnlyDictionary<FederatedMediaRef, FederatedMediaResolutionResult>> ResolveManyAsync(
        Guid peerServerId,
        IReadOnlyList<FederatedMediaRef> mediaRefs,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<FederatedMediaRef, FederatedMediaResolutionResult>(mediaRefs.Count);

        foreach (var mediaRef in mediaRefs)
        {
            var resolved = await ResolveSingleAsync(peerServerId, mediaRef, cancellationToken);
            results[mediaRef] = resolved;
        }

        return results;
    }

    private async Task<FederatedMediaResolutionResult> ResolveSingleAsync(
        Guid peerServerId,
        FederatedMediaRef mediaRef,
        CancellationToken cancellationToken)
    {
        foreach (var provider in ProviderPriority)
        {
            var externalId = mediaRef.ExternalIds
                .FirstOrDefault(e => string.Equals(e.Provider, provider, StringComparison.OrdinalIgnoreCase));

            if (externalId is null)
                continue;

            var mediaId = await context.ExternalIds
                .Where(e => e.ProviderName == externalId.Provider
                    && e.Value == externalId.Value
                    && e.MediaId != null
                    && e.ProviderName != "federation")
                .Select(e => e.MediaId)
                .FirstOrDefaultAsync(cancellationToken);

            if (mediaId.HasValue)
            {
                return new FederatedMediaResolutionResult
                {
                    Status = FederatedMediaResolutionStatus.ResolvedLocal,
                    LocalMediaId = mediaId.Value
                };
            }
        }

        var federationValue = $"{peerServerId}:{mediaRef.RemoteMediaId}";
        var federationMediaId = await context.ExternalIds
            .Where(e => e.ProviderName == "federation" && e.Value == federationValue && e.MediaId != null)
            .Select(e => e.MediaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (federationMediaId.HasValue)
        {
            var remoteFileId = await context.RemoteIndexedFiles
                .Where(f => f.PeerServerId == peerServerId && f.RemoteMediaId == mediaRef.RemoteMediaId)
                .Select(f => (Guid?)f.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (remoteFileId.HasValue)
            {
                return new FederatedMediaResolutionResult
                {
                    Status = FederatedMediaResolutionStatus.ResolvedRemote,
                    LocalMediaId = federationMediaId.Value,
                    RemoteIndexedFileId = remoteFileId.Value
                };
            }

            return new FederatedMediaResolutionResult
            {
                Status = FederatedMediaResolutionStatus.ResolvedLocal,
                LocalMediaId = federationMediaId.Value
            };
        }

        var remoteOnly = await context.RemoteIndexedFiles
            .Where(f => f.PeerServerId == peerServerId && f.RemoteMediaId == mediaRef.RemoteMediaId)
            .Select(f => new { f.Id, f.MediaId })
            .FirstOrDefaultAsync(cancellationToken);

        if (remoteOnly is not null)
        {
            return new FederatedMediaResolutionResult
            {
                Status = FederatedMediaResolutionStatus.ResolvedRemote,
                LocalMediaId = remoteOnly.MediaId,
                RemoteIndexedFileId = remoteOnly.Id
            };
        }

        return new FederatedMediaResolutionResult
        {
            Status = FederatedMediaResolutionStatus.Unavailable
        };
    }
}
