using K7.Server.Application.Features.SmartPlaylists.Commands.EvaluateSmartPlaylist;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.SmartPlaylists;

public class EvaluateSmartPlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/smart-playlists/{id}/evaluate", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken = default) =>
        {
            var result = await sender.Send(new EvaluateSmartPlaylistCommand
            {
                Id = id
            }, cancellationToken);

            return Results.Ok(new { Id = result });
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
