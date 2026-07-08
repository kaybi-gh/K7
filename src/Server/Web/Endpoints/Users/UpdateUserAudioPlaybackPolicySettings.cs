using K7.Server.Application.Features.PlaybackPolicySettings.Commands.UpdateUserAudioPlaybackPolicySettings;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UpdateUserAudioPlaybackPolicySettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/me/preferences/audio-playback-policy", async (
            [FromBody] AudioPlaybackPolicySettingsDto settings,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateUserAudioPlaybackPolicySettingsCommand { Settings = settings }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
