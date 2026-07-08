using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetFederationPrivacySettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/federation-privacy", async (
            [FromServices] IUserFederationPrivacyService privacyService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            var settings = await privacyService.GetPrivacyAsync(userId, cancellationToken);
            return Results.Ok(settings);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class UpdateFederationPrivacySettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/me/federation-privacy", async (
            [FromBody] FederationPrivacySettingsDto settings,
            [FromServices] IUserFederationPrivacyService privacyService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            await privacyService.SetPrivacyAsync(userId, settings, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class GetReviewPreferences : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/review-preferences", async (
            [FromServices] IUserFederationPrivacyService privacyService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            var settings = await privacyService.GetReviewPreferencesAsync(userId, cancellationToken);
            return Results.Ok(settings);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class UpdateReviewPreferences : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/me/review-preferences", async (
            [FromBody] ReviewPreferencesDto settings,
            [FromServices] IUserFederationPrivacyService privacyService,
            [FromServices] IUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Id is not { } userId)
                return Results.Unauthorized();

            await privacyService.SetReviewPreferencesAsync(userId, settings, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
