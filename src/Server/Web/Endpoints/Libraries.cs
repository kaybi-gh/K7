using System.Threading;
using K7.Server.Application.Common.Models.Dtos;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using K7.Server.Application.Features.Libraries.Commands.DeleteLibrary;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Application.Features.Libraries.Commands.UpdateLibrary;
using K7.Server.Application.Features.Libraries.Queries.GetLibraries;
using K7.Server.Application.Features.Libraries.Queries.GetLibrary;
using K7.Server.Domain.Entities;

namespace K7.Server.Web.Endpoints;

public class Libraries : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetLibrary, "{id}")
            .MapGet(GetLibraries)
            .MapPost(CreateLibrary)
            .MapPost(IndexLibraryFiles, "{id}/index-files")
            .MapPut(UpdateLibrary, "{id}")
            .MapDelete(DeleteLibrary, "{id}");
    }

    public async Task<LibraryDto> GetLibrary(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        return await sender.Send(new GetLibraryQuery(id), cancellationToken);
    }

    public async Task<IEnumerable<LibraryDto>> GetLibraries(ISender sender, CancellationToken cancellationToken)
    {
        return await sender.Send(new GetLibrariesQuery(), cancellationToken);
    }

    public async Task<Guid> CreateLibrary(ISender sender, CreateLibraryCommand command, CancellationToken cancellationToken)
    {
        return await sender.Send(command, cancellationToken);
    }

    public async Task<IResult> IndexLibraryFiles(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new CreateBackgroundTaskCommand()
        {
            Request = new IndexLibraryFilesCommand(id),
            Priority = Domain.Enums.BackgroundTaskPriority.Normal,
            TargetEntityId = id,
            TargetEntityTypeName = nameof(Library)
        }, cancellationToken);
        return Results.NoContent();
    }

    public async Task<IResult> UpdateLibrary(ISender sender, Guid id, UpdateLibraryCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id) return Results.BadRequest();
        await sender.Send(command, cancellationToken);
        return Results.NoContent();
    }

    public async Task<IResult> DeleteLibrary(ISender sender, Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteLibraryCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
