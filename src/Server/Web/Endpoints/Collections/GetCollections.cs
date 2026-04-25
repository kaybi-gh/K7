using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Collections.Queries.GetCollections;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities.Collections;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Collections;

public class GetCollections : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/collections", async ([FromServices] ISender sender, [AsParameters] GetCollectionsWithPaginationQuery query, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(query, cancellationToken);
            return new
            {
                result.PageNumber,
                result.TotalPages,
                result.TotalCount,
                Items = result.Items.Select(c => c.ToLiteCollectionDto())
            };
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
