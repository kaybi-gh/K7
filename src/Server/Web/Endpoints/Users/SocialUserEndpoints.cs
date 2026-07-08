using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Federation.Commands.CopyFederatedPlaylist;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetSocialUserDirectoryEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/social/directory", async (
            [FromServices] ISocialUserProfileService profileService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            var entries = await profileService.GetDirectoryAsync(userId, cancellationToken);
            return Results.Ok(entries);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetLocalSocialUserProfileEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/{userId:guid}/social-profile", async (
            Guid userId,
            [FromServices] ISocialUserProfileService profileService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } viewerUserId)
                return Results.Unauthorized();

            var profile = await profileService.GetLocalProfileAsync(userId, viewerUserId, cancellationToken);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetSharedCollectionsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/shared-collections", async (
            [FromServices] ISocialUserProfileService profileService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            var collections = await profileService.GetSharedCollectionsAsync(userId, cancellationToken);
            return Results.Ok(collections);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetSharedPlaylistsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/shared-playlists", async (
            [FromServices] ISocialUserProfileService profileService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            var playlists = await profileService.GetSharedPlaylistsAsync(userId, cancellationToken);
            return Results.Ok(playlists);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetSocialDiscoveryStateEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/social/discovery", async (
            [FromServices] ISocialUserProfileService profileService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            var showDirectory = await profileService.IsDirectoryVisibleAsync(userId, cancellationToken);
            return Results.Ok(new SocialDiscoveryStateDto { ShowDirectory = showDirectory });
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
