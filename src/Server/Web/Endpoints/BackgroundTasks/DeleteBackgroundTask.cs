using K7.Server.Application.Features.BackgroundTasks.Commands.DeleteBackgroundTask;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.BackgroundTasks;

public class DeleteBackgroundTask : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/background-tasks/{id}", async ([FromServices] ISender sender, [FromQuery] Guid id, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteBackgroundTaskCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
