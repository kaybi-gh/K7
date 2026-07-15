using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Federation.Queries.GetRemoteStreamContent;

public record GetRemoteStreamContentQuery(Guid SessionId, string Path, string QueryString) : IRequest<RemoteStreamProxyResult>;

public record RemoteStreamProxyResult(
    int StatusCode,
    Stream? Body,
    string? ContentType,
    long? ContentLength,
    IReadOnlyDictionary<string, string[]> ForwardHeaders);

public class GetRemoteStreamContentQueryHandler(
    IApplicationDbContext context,
    IPeerAuthorizationService peerAuthorization,
    IPeerClient peerClient)
    : IRequestHandler<GetRemoteStreamContentQuery, RemoteStreamProxyResult>
{
    public async Task<RemoteStreamProxyResult> Handle(
        GetRemoteStreamContentQuery request,
        CancellationToken cancellationToken)
    {
        var session = await context.StreamSessions
            .Include(s => s.PeerServer)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.RemoteSessionId != null, cancellationToken);

        if (session?.PeerServer is null || session.RemoteSessionId is null)
            throw new NotFoundException(request.SessionId.ToString(), "StreamSession");

        if (session.PeerServer.Status != PeerStatus.Active)
            throw new PeerServerUnavailableException("Peer server is not active");

        var auth = await peerAuthorization.AuthenticateOutboundAsync(session.PeerServerId!.Value, cancellationToken);
        if (auth is null)
            throw new HttpRequestException("Failed to authenticate with peer.");

        var (peer, token) = auth.Value;
        var fullPath = $"{request.Path}{request.QueryString}";

        var response = await peerClient.ProxyStreamContentAsync(
            peer.BaseUrl, token, session.RemoteSessionId.Value, fullPath, cancellationToken);

        var forwardHeaders = new Dictionary<string, string[]>();
        foreach (var header in response.Headers)
        {
            if (header.Key.Equals("Accept-Ranges", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Content-Range", StringComparison.OrdinalIgnoreCase))
            {
                forwardHeaders[header.Key] = header.Value.ToArray();
            }
        }

        Stream? body = null;
        if (response.IsSuccessStatusCode)
            body = await response.Content.ReadAsStreamAsync(cancellationToken);

        return new RemoteStreamProxyResult(
            (int)response.StatusCode,
            body,
            response.Content.Headers.ContentType?.ToString(),
            response.Content.Headers.ContentLength,
            forwardHeaders);
    }
}
