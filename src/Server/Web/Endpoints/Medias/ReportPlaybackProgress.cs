using K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class ReportPlaybackProgress : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/medias/playback-progress", async ([FromServices] ISender sender, [FromBody] UpdatePlaybackProgressCommand command) =>
        {
            await sender.Send(command);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
