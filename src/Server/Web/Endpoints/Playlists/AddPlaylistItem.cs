using K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Playlists;

public class AddPlaylistItem : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/playlists/{playlistId}/items", async ([FromServices] ISender sender, Guid playlistId, [FromBody] AddPlaylistItemRequest request, CancellationToken cancellationToken) =>
        {
            var itemId = await sender.Send(new AddPlaylistItemCommand
            {
                PlaylistId = playlistId,
                MediaId = request.MediaId
            }, cancellationToken);

            return Results.Created($"/api/playlists/{playlistId}", itemId);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(type.Namespace!.Split('.').Last());
    }
}

public record AddPlaylistItemRequest(Guid MediaId);
