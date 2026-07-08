using K7.Server.Application.Features.AudioPlayerSettings.Commands.UpdateUserAudioPlayerSettings;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UpdateUserAudioPlayerSettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/me/preferences/audio-player", async (
            [FromBody] AudioPlayerSettingsDto settings,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateUserAudioPlayerSettingsCommand { Settings = settings }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
