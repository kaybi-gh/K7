using K7.Server.Application.Features.Users.Commands.VerifyUserPin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using K7.Server.Web.Infrastructure;

namespace K7.Server.Web.Endpoints.Users;

public class VerifyUserPin : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/users/{userId:guid}/verify-pin", async (
            [FromRoute] Guid userId,
            [FromBody] VerifyUserPinRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var isValid = await sender.Send(new VerifyUserPinCommand(userId, request.Pin), cancellationToken);
            return isValid ? Results.Ok() : Results.Unauthorized();
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingExtensions.PinVerifyPolicy)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public sealed record VerifyUserPinRequest(string Pin);
