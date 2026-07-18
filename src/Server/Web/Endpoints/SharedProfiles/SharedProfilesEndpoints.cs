using K7.Server.Application.Features.SharedProfiles.Commands.AssignSharedProfileContentRestriction;
using K7.Server.Application.Features.SharedProfiles.Commands.CreateSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.DeleteSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.DeleteSharedProfileHomeLayout;
using K7.Server.Application.Features.SharedProfiles.Commands.LeaveSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.SetSharedProfilePin;
using K7.Server.Application.Features.SharedProfiles.Commands.SharePlaylistToSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.UnsharePlaylistFromSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileAudioPlaybackPolicy;
using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileHomeLayout;
using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileVideoPlaybackPolicy;
using K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfileHomeLayout;
using K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfileMemberCandidates;
using K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfiles;
using K7.Server.Application.Features.SharedProfiles.Commands.VerifySharedProfilePin;
using K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfileAudioPlaybackPolicy;
using K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfilePlaylistIds;
using K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfileVideoPlaybackPolicy;
using K7.Server.Domain.Constants;
using K7.Server.Web.Infrastructure;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace K7.Server.Web.Endpoints.SharedProfiles;

public class GetSharedProfiles : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/shared-profiles", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetSharedProfilesQuery(), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetSharedProfileMemberCandidates : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/shared-profiles/member-candidates", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetSharedProfileMemberCandidatesQuery(), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class CreateSharedProfile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/shared-profiles", async (
            [FromServices] ISender sender,
            [FromBody] CreateSharedProfileRequest request,
            CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new CreateSharedProfileCommand
            {
                Name = request.Name,
                HostUserId = request.HostUserId,
                MemberUserIds = request.MemberUserIds,
                Pin = request.Pin
            }, cancellationToken);
            return Results.Created($"/api/shared-profiles/{id}", id);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class UpdateSharedProfile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/shared-profiles/{id:guid}", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] UpdateSharedProfileRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateSharedProfileCommand
            {
                Id = id,
                Name = request.Name,
                HostUserId = request.HostUserId,
                MemberUserIds = request.MemberUserIds
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class DeleteSharedProfile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/shared-profiles/{id:guid}", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteSharedProfileCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class SetSharedProfilePin : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/shared-profiles/{id:guid}/pin", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] SetSharedProfilePinRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new SetSharedProfilePinCommand(id, request.Pin), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class LeaveSharedProfile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/shared-profiles/{id:guid}/leave", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] LeaveSharedProfileRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new LeaveSharedProfileCommand(id, request.NewHostUserId), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class VerifySharedProfilePinEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/shared-profiles/{id:guid}/verify-pin", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] VerifySharedProfilePinRequest request,
            CancellationToken cancellationToken) =>
        {
            var isValid = await sender.Send(new VerifySharedProfilePinCommand(id, request.Pin), cancellationToken);
            return isValid ? Results.Ok() : Results.Unauthorized();
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingExtensions.PinVerifyPolicy)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public sealed record VerifySharedProfilePinRequest(string Pin);

public sealed record AssignSharedProfileContentRestrictionRequest(Guid? ContentRestrictionProfileId);

public class GetSharedProfileVideoPlaybackPolicyEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/shared-profiles/{id:guid}/video-playback-policy", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
            await sender.Send(new GetSharedProfileVideoPlaybackPolicyQuery(id), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class UpdateSharedProfileVideoPlaybackPolicyEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/shared-profiles/{id:guid}/video-playback-policy", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] VideoPlaybackPolicySettingsDto settings,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateSharedProfileVideoPlaybackPolicyCommand
            {
                SharedProfileId = id,
                Settings = settings
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetSharedProfileAudioPlaybackPolicyEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/shared-profiles/{id:guid}/audio-playback-policy", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
            await sender.Send(new GetSharedProfileAudioPlaybackPolicyQuery(id), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class UpdateSharedProfileAudioPlaybackPolicyEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/shared-profiles/{id:guid}/audio-playback-policy", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] AudioPlaybackPolicySettingsDto settings,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateSharedProfileAudioPlaybackPolicyCommand
            {
                SharedProfileId = id,
                Settings = settings
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class AssignSharedProfileContentRestrictionEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/shared-profiles/{id:guid}/content-restriction", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] AssignSharedProfileContentRestrictionRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new AssignSharedProfileContentRestrictionCommand
            {
                SharedProfileId = id,
                ContentRestrictionProfileId = request.ContentRestrictionProfileId
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetSharedProfilePlaylistIdsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/shared-profiles/{id:guid}/playlists", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
            await sender.Send(new GetSharedProfilePlaylistIdsQuery(id), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class SharePlaylistToSharedProfileEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/shared-profiles/{id:guid}/playlists/{playlistId:guid}", async (
            [FromServices] ISender sender,
            Guid id,
            Guid playlistId,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new SharePlaylistToSharedProfileCommand
            {
                SharedProfileId = id,
                PlaylistId = playlistId
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class UnsharePlaylistFromSharedProfileEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/shared-profiles/{id:guid}/playlists/{playlistId:guid}", async (
            [FromServices] ISender sender,
            Guid id,
            Guid playlistId,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UnsharePlaylistFromSharedProfileCommand
            {
                SharedProfileId = id,
                PlaylistId = playlistId
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetSharedProfileHomeLayoutEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/shared-profiles/{id:guid}/home-layout", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetSharedProfileHomeLayoutQuery(id), cancellationToken);
            return result is not null ? Results.Ok(result) : Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class UpdateSharedProfileHomeLayoutEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/shared-profiles/{id:guid}/home-layout", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] HomeLayoutDto layout,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateSharedProfileHomeLayoutCommand
            {
                SharedProfileId = id,
                Layout = layout
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class DeleteSharedProfileHomeLayoutEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/shared-profiles/{id:guid}/home-layout", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteSharedProfileHomeLayoutCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

