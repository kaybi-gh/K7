using K7.Server.Application.Features.Playlists.Commands.UpdatePlaylist;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Playlists;

public class UpdatePlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/playlists/{id}", async ([FromServices] ISender sender, Guid id, UpdatePlaylistCommand command, CancellationToken cancellationToken) =>
        {
            if (id != command.Id) return Results.BadRequest();
            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
