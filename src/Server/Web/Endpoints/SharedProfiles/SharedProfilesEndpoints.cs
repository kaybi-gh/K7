using K7.Server.Application.Features.SharedProfiles.Commands.CreateSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.DeleteSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.LeaveSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Commands.SetSharedProfilePin;
using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfile;
using K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfileMemberCandidates;
using K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfiles;
using K7.Server.Application.Features.SharedProfiles.Commands.VerifySharedProfilePin;
using K7.Server.Domain.Constants;
using K7.Server.Web.Infrastructure;
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
