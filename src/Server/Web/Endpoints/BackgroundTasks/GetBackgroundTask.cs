using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.BackgroundTasks.Queries.GetBackgroundTask;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.BackgroundTasks;

public class GetBackgroundTask : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/background-tasks/{id}", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            var backgroundTask = await sender.Send(new GetBackgroundTaskQuery(id), cancellationToken);
            return backgroundTask.ToBackgroundTaskDto();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
