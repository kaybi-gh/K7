using K7.Server.Application.Features.PlaybackPolicySettings.Commands.UpdateDefaultAudioPlaybackPolicySettings;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdateServerAudioPlaybackPolicySettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/server/preferences/audio-playback-policy", async (
            [FromBody] AudioPlaybackPolicySettingsDto settings,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateDefaultAudioPlaybackPolicySettingsCommand { Settings = settings }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
