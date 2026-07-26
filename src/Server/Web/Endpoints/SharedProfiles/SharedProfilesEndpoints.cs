using K7.Server.Application.Features.SharedProfiles.Commands.AssignSharedProfileContentRestriction;
using K7.Server.Application.Features.SharedProfiles.Commands.CreateSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.DeleteSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.DeleteSharedProfileHomeLayout;
using K7.Server.Application.Features.SharedProfiles.Commands.LeaveSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.RemoveSharedProfileAvatar;
using K7.Server.Application.Features.SharedProfiles.Commands.SetSharedProfilePin;
using K7.Server.Application.Features.SharedProfiles.Commands.SharePlaylistToSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.UnsharePlaylistFromSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileAudioPlaybackPolicy;
using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileHomeLayout;
using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileVideoPlaybackPolicy;
using K7.Server.Application.Features.SharedProfiles.Commands.UploadSharedProfileAvatar;
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

public class UploadSharedProfileAvatarEndpoint : IEndpoint
{
    private const long MaxFileSize = 2 * 1024 * 1024;

    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/shared-profiles/{id:guid}/avatar", async (
            HttpContext context,
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file");

            if (file is null)
                return Results.BadRequest("No file provided.");

            if (file.Length > MaxFileSize)
                return Results.BadRequest("File size exceeds 2MB limit.");

            if (!IsImageFile(file.ContentType, file.FileName))
                return Results.BadRequest("File must be an image.");

            await using var stream = file.OpenReadStream();
            if (!await HasValidImageSignatureAsync(stream, cancellationToken))
                return Results.BadRequest("File must be a valid image.");

            await sender.Send(new UploadSharedProfileAvatarCommand
            {
                SharedProfileId = id,
                FileStream = stream,
                FileName = Path.ChangeExtension(file.FileName, Path.GetExtension(file.FileName).ToLowerInvariant())
            }, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .DisableAntiforgery()
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static bool IsImageFile(string? contentType, string fileName)
    {
        if (contentType is not null && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return true;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp";
    }

    private static async Task<bool> HasValidImageSignatureAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        stream.Position = 0;

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;

        if (read >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return true;

        if (read >= 6
            && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46
            && header[3] == 0x38 && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61)
            return true;

        if (read >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return true;

        return false;
    }
}

public class RemoveSharedProfileAvatarEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/shared-profiles/{id:guid}/avatar", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemoveSharedProfileAvatarCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

