using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Queries.GetMedias;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace K7.Server.Web.Endpoints.Medias;

public class GetMedias : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(GetMediasWithPaginationQueryUriBuilder.Route, async (
            HttpContext httpContext,
            [FromServices] ISender sender,
            [FromServices] IMediaQueryCacheInvalidator cacheInvalidator,
            [AsParameters] GetMediasWithPaginationQuery query,
            CancellationToken cancellationToken) =>
        {
            var etag = $"\"{cacheInvalidator.Version}\"";

            if (httpContext.Request.Headers.IfNoneMatch == etag)
                return Results.StatusCode(StatusCodes.Status304NotModified);

            var mediasPage = await sender.Send(query, cancellationToken);
            var result = new PaginatedListDto<LiteMediaDto>
            {
                Items = mediasPage.Items,
                PageNumber = mediasPage.PageNumber,
                TotalPages = mediasPage.TotalPages,
                TotalCount = mediasPage.TotalCount
            };

            httpContext.Response.Headers[HeaderNames.ETag] = etag;
            httpContext.Response.Headers["Accept-Query"] = "application/json";
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
