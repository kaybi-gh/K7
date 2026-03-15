using K7.Server.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Setup;

public class CompleteSetup : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/setup/complete", async (CompleteSetupRequest request, [FromServices] ISetupService setupService, CancellationToken cancellationToken) =>
        {
            var result = await setupService.CompleteSetupAsync(request.Email, request.Password, cancellationToken);

            return result.Succeeded
                ? Results.Ok()
                : Results.Conflict(new { result.Errors });
        })
        .AllowAnonymous()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record CompleteSetupRequest(string Email, string Password);
