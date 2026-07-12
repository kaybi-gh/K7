using K7.Server.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using K7.Server.Web.Infrastructure;

namespace K7.Server.Web.Endpoints.Setup;

public class CompleteSetup : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/setup/complete", async (CompleteSetupRequest request, [FromServices] ISetupService setupService, CancellationToken cancellationToken) =>
        {
            var result = await setupService.CompleteSetupAsync(request.Email, request.Password, request.SetupToken, cancellationToken);

            return result.Succeeded
                ? Results.Ok()
                : Results.Conflict(new { result.Errors });
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record CompleteSetupRequest(string Email, string Password, string? SetupToken);
