using K7.Server.Application.Features.BackgroundTasks.Queries.GetBackgroundTasksWithPagination;
using K7.Server.Domain.Constants;
using K7.Server.Web.Converters;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.BackgroundTasks;

public class GetBackgroundTasks : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/background-tasks", async ([FromServices] ISender sender, [AsParameters] GetBackgroundTasksWithPaginationQuery query, CancellationToken cancellationToken) =>
        {
            var backgroundTasksPage = await sender.Send(query, cancellationToken);
            return backgroundTasksPage.ToDto(BackgroundTaskDto.FromDomain);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
