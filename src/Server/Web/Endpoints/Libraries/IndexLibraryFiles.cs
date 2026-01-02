using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class IndexLibraryFiles : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/libraries/{id}/index-files", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            await sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new IndexLibraryFilesCommand(id),
                Priority = Domain.Enums.BackgroundTaskPriority.Normal,
                TargetEntityId = id,
                TargetEntityTypeName = nameof(Library)
            }, cancellationToken);
            return Results.NoContent();
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
