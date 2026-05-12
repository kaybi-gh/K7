using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;
using K7.Server.Domain.Constants;
using K7.Server.Web.Converters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace K7.Server.Web.Endpoints.Home;

public class GetHomeFeed : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/home/feed", async (
            HttpContext httpContext,
            [FromServices] ISender sender,
            [FromServices] IMediaQueryCacheInvalidator cacheInvalidator,
            [AsParameters] GetHomeFeedItemsQuery query) =>
        {
            var etag = $"\"{cacheInvalidator.Version}\"";

            if (httpContext.Request.Headers.IfNoneMatch == etag)
                return Results.StatusCode(StatusCodes.Status304NotModified);

            var result = await sender.Send(query);

            httpContext.Response.Headers[HeaderNames.ETag] = etag;
            return Results.Ok(result.ToDto(x => x));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
