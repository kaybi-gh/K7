using K7.Server.Application.Features.Playlists.Commands.ReorderPlaylistItem;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Playlists;

public class ReorderPlaylistItem : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();

        endpointRouteBuilder.MapPut("/api/playlists/{playlistId}/items/{itemId}/reorder", async ([FromServices] ISender sender, Guid playlistId, Guid itemId, [FromBody] ReorderPlaylistItemRequest request, CancellationToken cancellationToken) =>
        {
            await sender.Send(new ReorderPlaylistItemCommand
            {
                PlaylistId = playlistId,
                ItemId = itemId,
                NewOrder = request.NewOrder
            }, cancellationToken);

            return Results.NoContent();
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(type.Namespace!.Split('.').Last());
    }
}

public record ReorderPlaylistItemRequest(int NewOrder);
