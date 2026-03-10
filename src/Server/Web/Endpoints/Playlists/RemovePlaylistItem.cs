using K7.Server.Application.Features.Playlists.Commands.RemovePlaylistItem;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Playlists;

public class RemovePlaylistItem : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/playlists/{playlistId}/items/{itemId}", async ([FromServices] ISender sender, Guid playlistId, Guid itemId, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemovePlaylistItemCommand
            {
                PlaylistId = playlistId,
                ItemId = itemId
            }, cancellationToken);

            return Results.NoContent();
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
