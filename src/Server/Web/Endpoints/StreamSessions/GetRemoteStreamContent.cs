using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.StreamSessions;

public class GetRemoteStreamContent : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/api/remote-stream-sessions/{sessionId:guid}/{**path}", ["GET", "HEAD"], async (
            Guid sessionId,
            string path,
            [FromServices] IApplicationDbContext context,
            [FromServices] IPeerClient peerClient,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var session = await context.StreamSessions
                .Include(s => s.PeerServer)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.RemoteSessionId != null, cancellationToken);

            if (session?.PeerServer is null || session.RemoteSessionId is null)
                return Results.NotFound();

            var peer = session.PeerServer;
            if (peer.Status != PeerStatus.Active)
                return Results.Problem("Peer server is not active", statusCode: 503);

            var token = await peerClient.GetAccessTokenAsync(
                peer.BaseUrl, peer.OutboundClientId!, peer.OutboundClientSecret!, cancellationToken);

            if (token is null)
                return Results.Problem("Failed to authenticate with peer", statusCode: 502);

            // Build path with query string
            var queryString = httpContext.Request.QueryString.Value ?? "";
            var fullPath = $"{path}{queryString}";

            var response = await peerClient.ProxyStreamContentAsync(
                peer.BaseUrl, token, session.RemoteSessionId.Value, fullPath, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Results.StatusCode((int)response.StatusCode);
            }

            // Forward response headers
            httpContext.Response.StatusCode = (int)response.StatusCode;

            if (response.Content.Headers.ContentType is not null)
            {
                httpContext.Response.ContentType = response.Content.Headers.ContentType.ToString();
            }

            if (response.Content.Headers.ContentLength is not null)
            {
                httpContext.Response.ContentLength = response.Content.Headers.ContentLength;
            }

            foreach (var header in response.Headers)
            {
                if (header.Key.Equals("Accept-Ranges", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Content-Range", StringComparison.OrdinalIgnoreCase))
                {
                    httpContext.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            // Stream the response body without buffering
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await stream.CopyToAsync(httpContext.Response.Body, cancellationToken);

            return Results.Empty;
        })
        .RequireAuthorization(Policies.StreamAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
