using K7.Server.Application.Features.Federation.Queries.GetFederationGrantTargets;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederationSocialCollectionsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/collections", async (
            [FromServices] IFederationSocialConsumerService consumerService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            var collections = await consumerService.GetCollectionsAsync(userId, cancellationToken);
            return Results.Ok(collections);
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetFederationSocialPlaylistsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/playlists", async (
            [FromServices] IFederationSocialConsumerService consumerService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            var playlists = await consumerService.GetPlaylistsAsync(userId, cancellationToken);
            return Results.Ok(playlists);
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetFederationSocialSmartPlaylistsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/smart-playlists", async (
            [FromServices] IFederationSocialConsumerService consumerService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            var playlists = await consumerService.GetSmartPlaylistsAsync(userId, cancellationToken);
            return Results.Ok(playlists);
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetFederationPlaybackHistoryEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/playback-history", async (
            [FromServices] IFederationSocialConsumerService consumerService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            var history = await consumerService.GetPlaybackHistoryAsync(userId, cancellationToken);
            return Results.Ok(history);
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetFederationGrantTargetsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/federation-privacy/grant-targets", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var targets = await sender.Send(new GetFederationGrantTargetsQuery(), cancellationToken);
            return Results.Ok(targets);
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags("Users");
    }
}
