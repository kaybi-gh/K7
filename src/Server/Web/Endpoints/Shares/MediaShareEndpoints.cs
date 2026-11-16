using K7.Server.Application.Features.MediaShares.Commands.CreateMediaShare;
using K7.Server.Application.Features.MediaShares.Commands.DeleteMediaShare;
using K7.Server.Application.Features.MediaShares.Commands.RegenerateMediaShareLink;
using K7.Server.Application.Features.MediaShares.Commands.RevokeMediaShare;
using K7.Server.Application.Features.MediaShares.Commands.UpdateMediaShare;
using K7.Server.Application.Features.MediaShares.Queries.GetMediaShare;
using K7.Server.Application.Features.MediaShares.Queries.GetMediaShares;
using K7.Server.Application.Features.MediaShares.Queries.GetMediaSharingAvailability;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Shares;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Shares;

/// <summary>
/// Share management for signed-in users. Handlers enforce the share capability and
/// ownership: administrators manage every share, others only their own.
/// </summary>
public class MediaShareEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/shares", async (
            [FromServices] ISender sender,
            [FromQuery] bool? all,
            [FromQuery] Guid? mediaId,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new GetMediaSharesQuery { All = all ?? false, MediaId = mediaId },
                cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("GetMediaShares")
        .WithTags(groupName);

        endpointRouteBuilder.MapGet("/api/shares/availability", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetMediaSharingAvailabilityQuery(), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName("GetMediaSharingAvailability")
        .WithTags(groupName);

        endpointRouteBuilder.MapGet("/api/shares/{id:guid}", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetMediaShareQuery(id), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("GetMediaShare")
        .WithTags(groupName);

        endpointRouteBuilder.MapPost("/api/shares", async (
            [FromBody] CreateMediaShareRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new CreateMediaShareCommand
                {
                    MediaId = request.MediaId,
                    Label = request.Label,
                    Password = request.Password,
                    ExpiresAt = request.ExpiresAt,
                    MaxAccessCount = request.MaxAccessCount,
                    AllowPlayback = request.AllowPlayback,
                    AllowDownload = request.AllowDownload,
                    CloseWhenConsumed = request.CloseWhenConsumed
                },
                cancellationToken);
            return Results.Created($"/api/shares/{result.Id}", result);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("CreateMediaShare")
        .WithTags(groupName);

        endpointRouteBuilder.MapPut("/api/shares/{id:guid}", async (
            Guid id,
            [FromBody] UpdateMediaShareRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(
                new UpdateMediaShareCommand
                {
                    Id = id,
                    Label = request.Label,
                    Password = request.Password,
                    ExpiresAt = request.ExpiresAt,
                    MaxAccessCount = request.MaxAccessCount,
                    AllowPlayback = request.AllowPlayback,
                    AllowDownload = request.AllowDownload,
                    CloseWhenConsumed = request.CloseWhenConsumed
                },
                cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("UpdateMediaShare")
        .WithTags(groupName);

        endpointRouteBuilder.MapPost("/api/shares/{id:guid}/revoke", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RevokeMediaShareCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("RevokeMediaShare")
        .WithTags(groupName);

        endpointRouteBuilder.MapPost("/api/shares/{id:guid}/regenerate-link", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new RegenerateMediaShareLinkCommand(id), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("RegenerateMediaShareLink")
        .WithTags(groupName);

        endpointRouteBuilder.MapDelete("/api/shares/{id:guid}", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteMediaShareCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("DeleteMediaShare")
        .WithTags(groupName);
    }
}
