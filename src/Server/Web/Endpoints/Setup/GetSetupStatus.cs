using K7.Server.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Setup;

public class GetSetupStatus : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/setup/status", async ([FromServices] ISetupService setupService, CancellationToken cancellationToken) =>
        {
            var isCompleted = await setupService.IsSetupCompletedAsync(cancellationToken);
            var requiresSetupToken = await setupService.RequiresSetupTokenAsync(cancellationToken);
            return Results.Ok(new { isCompleted, requiresSetupToken });
        })
        .AllowAnonymous()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
