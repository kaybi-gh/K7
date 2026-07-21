using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Federation.Commands.CopyFederatedPlaylist;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederatedSocialUserProfileEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/peers/{peerServerId:guid}/users/{originUserId:guid}/social-profile", async (
            Guid peerServerId,
            Guid originUserId,
            [FromServices] ISocialUserProfileService profileService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } viewerUserId)
                return Results.Unauthorized();

            var profile = await profileService.GetFederatedProfileAsync(peerServerId, originUserId, viewerUserId, cancellationToken);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class CopyFederatedPlaylistEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/peers/{peerServerId:guid}/users/{originUserId:guid}/playlists/{playlistId:guid}/copy", async (
            Guid peerServerId,
            Guid originUserId,
            Guid playlistId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new CopyFederatedPlaylistCommand(peerServerId, originUserId, playlistId), cancellationToken);
            return Results.Ok(id);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
