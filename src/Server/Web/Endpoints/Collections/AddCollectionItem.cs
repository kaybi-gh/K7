using K7.Server.Application.Features.Collections.Commands.AddCollectionItem;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Collections;

public class AddCollectionItem : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/collections/{collectionId}/items", async ([FromServices] ISender sender, Guid collectionId, [FromBody] AddCollectionItemRequest request, CancellationToken cancellationToken) =>
        {
            var itemId = await sender.Send(new AddCollectionItemCommand
            {
                CollectionId = collectionId,
                MediaId = request.MediaId
            }, cancellationToken);

            return Results.Created($"/api/collections/{collectionId}", itemId);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(type.Namespace!.Split('.').Last());
    }
}

public record AddCollectionItemRequest(Guid MediaId);
