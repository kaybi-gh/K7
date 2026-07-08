using K7.Server.Application.Features.AudioPlayerSettings.Commands.DeleteUserAudioPlayerSettings;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class DeleteUserAudioPlayerSettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/users/me/preferences/audio-player", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteUserAudioPlayerSettingsCommand(), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
