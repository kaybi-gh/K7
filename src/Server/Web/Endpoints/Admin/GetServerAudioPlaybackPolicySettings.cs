using K7.Server.Application.Features.PlaybackPolicySettings.Queries.GetDefaultAudioPlaybackPolicySettings;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class GetServerAudioPlaybackPolicySettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/server/preferences/audio-playback-policy", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetDefaultAudioPlaybackPolicySettingsQuery(), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
