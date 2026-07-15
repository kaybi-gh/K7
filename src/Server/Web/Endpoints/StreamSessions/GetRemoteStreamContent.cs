using K7.Server.Application.Features.Federation.Queries.GetRemoteStreamContent;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

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
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var queryString = httpContext.Request.QueryString.Value ?? "";
            var result = await sender.Send(
                new GetRemoteStreamContentQuery(sessionId, path, queryString),
                cancellationToken);

            if (result.StatusCode is < 200 or >= 300)
                return Results.StatusCode(result.StatusCode);

            httpContext.Response.StatusCode = result.StatusCode;

            if (result.ContentType is not null)
                httpContext.Response.ContentType = result.ContentType;

            if (result.ContentLength is not null)
                httpContext.Response.ContentLength = result.ContentLength;

            foreach (var header in result.ForwardHeaders)
                httpContext.Response.Headers[header.Key] = header.Value;

            if (result.Body is not null)
                await result.Body.CopyToAsync(httpContext.Response.Body, cancellationToken);

            return Results.Empty;
        })
        .RequireAuthorization(Policies.StreamAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
