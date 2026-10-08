using K7.Server.Application.Common.Models;
using K7.Server.Application.Services;

namespace K7.Server.Application.Features.Federation.Queries.TryServeLocalFederatedHls;

/// <summary>
/// Returns local HLS content when the session uses Peer execution. Null means proxy to origin.
/// </summary>
public record TryServeLocalFederatedHlsQuery(Guid SessionId, string Path, string QueryString)
    : IRequest<HttpContentResult?>;

public class TryServeLocalFederatedHlsQueryHandler(IFederatedRequesterHlsService hlsService)
    : IRequestHandler<TryServeLocalFederatedHlsQuery, HttpContentResult?>
{
    public Task<HttpContentResult?> Handle(
        TryServeLocalFederatedHlsQuery request,
        CancellationToken cancellationToken) =>
        hlsService.TryServeAsync(request.SessionId, request.Path, request.QueryString, cancellationToken);
}
