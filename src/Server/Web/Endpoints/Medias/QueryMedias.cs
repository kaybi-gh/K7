using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Medias.Queries.GetMedias;
using K7.Server.Domain.Constants;
using K7.Server.Web.Converters;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace K7.Server.Web.Endpoints.Medias;

public class QueryMedias : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
#if GENERATING_OPENAPI
        return;
#endif
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods(GetMediasWithPaginationQueryUriBuilder.Route, [HttpMethods.Query], async (
            HttpContext httpContext,
            [FromServices] ISender sender,
            [FromServices] IMediaQueryCacheInvalidator cacheInvalidator,
            [FromServices] LiteMediaProjectionService liteMediaProjection,
            [FromBody] QueryMediasRequest request,
            CancellationToken cancellationToken) =>
        {
            var etag = $"\"{cacheInvalidator.Version}\"";

            if (httpContext.Request.Headers.IfNoneMatch == etag)
                return Results.StatusCode(StatusCodes.Status304NotModified);

            var mediasPage = await sender.Send(new QueryMediasQuery(request), cancellationToken);
            var liteItems = await liteMediaProjection.ToLiteListAsync(mediasPage.Items, cancellationToken);
            var result = new PaginatedListDto<LiteMediaDto>
            {
                Items = liteItems,
                PageNumber = mediasPage.PageNumber,
                TotalPages = mediasPage.TotalPages,
                TotalCount = mediasPage.TotalCount
            };

            httpContext.Response.Headers[HeaderNames.ETag] = etag;
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName)
        .Accepts<QueryMediasRequest>("application/json")
        .ExcludeFromDescription();
    }
}
