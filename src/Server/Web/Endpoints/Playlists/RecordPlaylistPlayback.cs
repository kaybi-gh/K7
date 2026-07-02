using K7.Server.Application.Features.Playlists.Commands.RecordPlaylistPlayback;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Playlists;

public class RecordPlaylistPlayback : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/playlists/{id:guid}/record-playback", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RecordPlaylistPlaybackCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
