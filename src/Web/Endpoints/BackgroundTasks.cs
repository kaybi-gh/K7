using MediaServer.Application.Common.Models.Dtos;
using MediaServer.Application.Features.BackgroundTasks.Commands.DeleteBackgroundTask;
using MediaServer.Application.Features.BackgroundTasks.Queries.GetBackgroundTask;
using MediaServer.Application.Features.BackgroundTasks.Queries.GetBackgroundTasksWithPagination;
using MediaServer.Application.Common.Models;

namespace MediaServer.Web.Endpoints;

public class BackgroundTasks : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetBackgroundTask, "{id}")
            .MapGet(GetBackgroundTasks)
            .MapDelete(DeleteBackgroundTask, "{id}");
    }

    public async Task<BackgroundTaskDto> GetBackgroundTask(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        return await sender.Send(new GetBackgroundTaskQuery(id), cancellationToken);
    }

    public async Task<PaginatedList<BackgroundTaskDto>> GetBackgroundTasks(ISender sender, [AsParameters] GetBackgroundTasksWithPaginationQuery query, CancellationToken cancellationToken)
    {
        return await sender.Send(query, cancellationToken);
    }

    public async Task<IResult> DeleteBackgroundTask(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteBackgroundTaskCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
