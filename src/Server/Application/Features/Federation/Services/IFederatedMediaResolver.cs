using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Services;

public enum FederatedMediaResolutionStatus
{
    ResolvedLocal,
    ResolvedRemote,
    Unavailable
}

public sealed record FederatedMediaResolutionResult
{
    public required FederatedMediaResolutionStatus Status { get; init; }
    public Guid? LocalMediaId { get; init; }
    public Guid? RemoteIndexedFileId { get; init; }
}

public interface IFederatedMediaResolver
{
    Task<FederatedMediaResolutionResult> ResolveAsync(
        Guid peerServerId,
        FederatedMediaRef mediaRef,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<FederatedMediaRef, FederatedMediaResolutionResult>> ResolveManyAsync(
        Guid peerServerId,
        IReadOnlyList<FederatedMediaRef> mediaRefs,
        CancellationToken cancellationToken = default);
}
