using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Collections.Queries.GetCollectionItems;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Collections;

public class GetCollectionItems : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/collections/{collectionId}/items", async ([FromServices] ISender sender, [AsParameters] GetCollectionItemsWithPaginationQuery query, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(query, cancellationToken);
            return new
            {
                result.PageNumber,
                result.TotalPages,
                result.TotalCount,
                Items = result.Items.Select(i => i.ToCollectionItemDto())
            };
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
