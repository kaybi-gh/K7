using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.MediaShares.Commands.EnsureShareVisitorDevice;
using K7.Server.Application.Features.MediaShares.Commands.OpenMediaShare;
using K7.Server.Application.Features.MediaShares.Commands.ReportShareProgress;
using K7.Server.Application.Features.MediaShares.Queries.GetSharedMedia;
using K7.Server.Application.Features.MediaShares.Queries.GetShareDownloadFile;
using K7.Server.Application.Features.MediaShares.Queries.GetShareVisitorSession;
using K7.Server.Application.Features.StreamSessions.Commands.CreateStreamSession;
using K7.Server.Domain.Constants;
using K7.Server.Web.Endpoints.StreamSessions;
using K7.Server.Web.Infrastructure;
using K7.Shared.Dtos.Shares;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace K7.Server.Web.Endpoints.Shares;

/// <summary>
/// What a public share visitor can call. Opening a share is anonymous and takes the link
/// token in the body (never in the URL, which ends up in logs). It sets the visitor
/// session cookie, which the other endpoints require.
/// </summary>
public class PublicShareEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/public/shares/open", async (
            HttpContext httpContext,
            [FromBody] OpenShareRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new OpenMediaShareCommand
                {
                    Token = request.Token,
                    Password = request.Password,
                    ExistingSessionTokens = ShareSessionCookie.Read(httpContext.Request),
                    IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = httpContext.Request.Headers.UserAgent.ToString()
                },
                cancellationToken);

            if (result.SessionToken is not null)
                ShareSessionCookie.Add(httpContext, result.SessionToken);

            return Results.Ok(result.Dto);
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingExtensions.ShareOpenPolicy)
        .WithName("OpenPublicShare")
        .WithTags(groupName);

        endpointRouteBuilder.MapGet("/api/public/shares/{shareId:guid}/session", async (
            Guid shareId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetShareVisitorSessionQuery(shareId), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.ShareAccess)
        .WithName("GetPublicShareSession")
        .WithTags(groupName);

        endpointRouteBuilder.MapGet("/api/public/shares/{shareId:guid}/medias/{mediaId:guid}", async (
            Guid shareId,
            Guid mediaId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var media = await sender.Send(new GetSharedMediaQuery(shareId, mediaId), cancellationToken);
            return media.ToMediaDto();
        })
        .RequireAuthorization(Policies.ShareAccess)
        .WithName("GetPublicShareMedia")
        .WithTags(groupName);

        endpointRouteBuilder.MapPost("/api/public/shares/{shareId:guid}/stream-sessions", async (
            Guid shareId,
            [FromBody] CreateShareStreamSessionRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var deviceId = await sender.Send(
                new EnsureShareVisitorDeviceCommand
                {
                    ShareId = shareId,
                    IndexedFileId = request.IndexedFileId,
                    SupportedContainers = request.SupportedContainers,
                    SupportedAudioCodecs = request.SupportedAudioCodecs,
                    SupportedVideoCodecs = request.SupportedVideoCodecs,
                    CapabilityTokens = request.CapabilityTokens,
                    SupportsHdr = request.SupportsHdr,
                    DisplayResolutionWidth = request.DisplayResolutionWidth,
                    DisplayResolutionHeight = request.DisplayResolutionHeight
                },
                cancellationToken);

            var session = await StreamSessionNegotiator.CreateAsync(
                sender,
                new CreateStreamSessionCommand
                {
                    IndexedFileId = request.IndexedFileId,
                    DeviceId = deviceId,
                    AudioTrackIndex = request.AudioTrackIndex,
                    SubtitleTrackIndex = request.SubtitleTrackIndex,
                    AudioPassthrough = false,
                    StartSeconds = request.StartSeconds
                },
                cancellationToken);

            return Results.Ok(session);
        })
        .RequireAuthorization(Policies.ShareAccess)
        .RequireRateLimiting(RateLimitingExtensions.ShareStreamPolicy)
        .WithName("CreatePublicShareStreamSession")
        .WithTags(groupName);

        endpointRouteBuilder.MapPost("/api/public/shares/{shareId:guid}/progress", async (
            Guid shareId,
            [FromBody] ReportShareProgressRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(
                new ReportShareProgressCommand
                {
                    ShareId = shareId,
                    MediaId = request.MediaId,
                    PositionSeconds = request.PositionSeconds,
                    DurationSeconds = request.DurationSeconds,
                    SessionId = request.SessionId,
                    ReferenceId = request.ReferenceId,
                    State = request.State,
                    IndexedFileId = request.IndexedFileId,
                    AudioTrackIndex = request.AudioTrackIndex,
                    SubtitleTrackIndex = request.SubtitleTrackIndex
                },
                cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.ShareAccess)
        .RequireRateLimiting(RateLimitingExtensions.ShareVisitorPolicy)
        .WithName("ReportPublicShareProgress")
        .WithTags(groupName);

        endpointRouteBuilder.MapMethods(
            "/api/public/shares/{shareId:guid}/files/{indexedFileId:guid}/download",
            ["GET", "HEAD"],
            async (
                HttpContext httpContext,
                Guid shareId,
                Guid indexedFileId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetShareDownloadFileQuery(shareId, indexedFileId, HttpMethods.IsGet(httpContext.Request.Method)),
                    cancellationToken);
                return result.ToIResult();
            })
        .RequireAuthorization(Policies.ShareAccess)
        .WithName("DownloadPublicShareFile")
        .WithTags(groupName);
    }
}
