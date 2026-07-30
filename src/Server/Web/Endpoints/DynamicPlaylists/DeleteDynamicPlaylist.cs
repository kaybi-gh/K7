using K7.Server.Application.Features.DynamicPlaylists.Commands.DeleteDynamicPlaylist;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.DynamicPlaylists;

public class DeleteDynamicPlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/dynamic-playlists/{id}", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteDynamicPlaylistCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
