using K7.Server.Application.Features.SmartPlaylists.Commands.DeleteSmartPlaylist;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.SmartPlaylists;

public class DeleteSmartPlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/smart-playlists/{id}", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteSmartPlaylistCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
