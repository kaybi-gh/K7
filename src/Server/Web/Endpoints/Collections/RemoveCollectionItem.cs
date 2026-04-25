using K7.Server.Application.Features.Collections.Commands.RemoveCollectionItem;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Collections;

public class RemoveCollectionItem : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/collections/{collectionId}/items/{itemId}", async ([FromServices] ISender sender, Guid collectionId, Guid itemId, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemoveCollectionItemCommand
            {
                CollectionId = collectionId,
                ItemId = itemId
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(type.Namespace!.Split('.').Last());
    }
}
