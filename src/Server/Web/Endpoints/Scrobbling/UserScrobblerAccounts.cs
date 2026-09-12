using K7.Server.Application.Features.Scrobbling.Commands.CompleteLastFmAuth;
using K7.Server.Application.Features.Scrobbling.Commands.CreateUserScrobblerAccount;
using K7.Server.Application.Features.Scrobbling.Commands.DeleteUserScrobblerAccount;
using K7.Server.Application.Features.Scrobbling.Commands.PollTraktDevice;
using K7.Server.Application.Features.Scrobbling.Commands.StartLastFmAuth;
using K7.Server.Application.Features.Scrobbling.Commands.StartTraktDevice;
using K7.Server.Application.Features.Scrobbling.Commands.TestUserScrobblerAccount;
using K7.Server.Application.Features.Scrobbling.Commands.UpdateUserScrobblerAccount;
using K7.Server.Application.Features.Scrobbling.Queries.GetScrobbleWebhookPresets;
using K7.Server.Application.Features.Scrobbling.Queries.GetUserScrobblerAccounts;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Scrobbling;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Scrobbling;

public class UserScrobblerAccounts : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapGet("/api/scrobbling/accounts", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
            await sender.Send(new GetUserScrobblerAccountsQuery(), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("GetUserScrobblerAccounts")
        .WithTags("Scrobbling");

        endpointRouteBuilder.MapPost("/api/scrobbling/accounts", async (
            [FromServices] ISender sender,
            CreateUserScrobblerAccountRequest request,
            CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new CreateUserScrobblerAccountCommand(request), cancellationToken);
            return Results.Created($"/api/scrobbling/accounts/{id}", id);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("CreateUserScrobblerAccount")
        .WithTags("Scrobbling");

        endpointRouteBuilder.MapPut("/api/scrobbling/accounts/{id:guid}", async (
            [FromServices] ISender sender,
            Guid id,
            UpdateUserScrobblerAccountRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateUserScrobblerAccountCommand(id, request), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("UpdateUserScrobblerAccount")
        .WithTags("Scrobbling");

        endpointRouteBuilder.MapDelete("/api/scrobbling/accounts/{id:guid}", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteUserScrobblerAccountCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("DeleteUserScrobblerAccount")
        .WithTags("Scrobbling");

        endpointRouteBuilder.MapPost("/api/scrobbling/accounts/{id:guid}/test", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
            await sender.Send(new TestUserScrobblerAccountCommand(id), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("TestUserScrobblerAccount")
        .WithTags("Scrobbling");

        endpointRouteBuilder.MapGet("/api/scrobbling/presets", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
            await sender.Send(new GetScrobbleWebhookPresetsQuery(), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("GetScrobbleWebhookPresets")
        .WithTags("Scrobbling");

        endpointRouteBuilder.MapPost("/api/scrobbling/lastfm/start", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
            await sender.Send(new StartLastFmAuthCommand(), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("StartLastFmAuth")
        .WithTags("Scrobbling");

        endpointRouteBuilder.MapPost("/api/scrobbling/lastfm/complete", async (
            [FromServices] ISender sender,
            LastFmCompleteRequest request,
            CancellationToken cancellationToken) =>
            await sender.Send(new CompleteLastFmAuthCommand(request.Token, request.MediaTypes), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("CompleteLastFmAuth")
        .WithTags("Scrobbling");

        endpointRouteBuilder.MapPost("/api/scrobbling/trakt/start", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
            await sender.Send(new StartTraktDeviceCommand(), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("StartTraktDevice")
        .WithTags("Scrobbling");

        endpointRouteBuilder.MapPost("/api/scrobbling/trakt/poll", async (
            [FromServices] ISender sender,
            TraktPollRequest request,
            CancellationToken cancellationToken) =>
            await sender.Send(new PollTraktDeviceCommand(request.DeviceCode, request.MediaTypes, request.DisplayName), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("PollTraktDevice")
        .WithTags("Scrobbling");
    }

    public sealed record LastFmCompleteRequest(string Token, IReadOnlyList<string>? MediaTypes = null);
    public sealed record TraktPollRequest(string DeviceCode, IReadOnlyList<string>? MediaTypes = null, string? DisplayName = null);
}
