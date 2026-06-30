using K7.Server.Application.Features.BackgroundTasks.Queries.GetBackgroundTaskSummary;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.BackgroundTasks;

public class GetBackgroundTaskSummary : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/background-tasks/summary", async (
            [FromServices] ISender sender,
            [AsParameters] GetBackgroundTaskSummaryQuery query,
            CancellationToken cancellationToken) =>
        {
            return await sender.Send(query, cancellationToken);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
