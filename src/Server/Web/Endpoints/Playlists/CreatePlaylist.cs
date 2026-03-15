using K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Playlists;

public class CreatePlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/playlists", async ([FromServices] ISender sender, CreatePlaylistCommand command, CancellationToken cancellationToken) =>
        {
            return await sender.Send(command, cancellationToken);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
