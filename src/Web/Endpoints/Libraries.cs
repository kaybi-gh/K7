using MediaServer.Application.Common.Models.Dtos;
using MediaServer.Application.Features.Libraries.Commands.CreateLibrary;
using MediaServer.Application.Features.Libraries.Commands.DeleteLibrary;
using MediaServer.Application.Features.Libraries.Commands.IndexLibraryFiles;
using MediaServer.Application.Features.Libraries.Commands.UpdateLibrary;
using MediaServer.Application.Features.Libraries.Queries.GetLibraries;
using MediaServer.Application.Features.Libraries.Queries.GetLibrary;

namespace MediaServer.Web.Endpoints;

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

    public async Task<LibraryDto> GetLibrary(ISender sender, Guid id)
    {
        return await sender.Send(new GetLibraryQuery(id));
    }

    public async Task<IEnumerable<LibraryDto>> GetLibraries(ISender sender)
    {
        return await sender.Send(new GetLibrariesQuery());
    }

    public async Task<Guid> CreateLibrary(ISender sender, CreateLibraryCommand command)
    {
        return await sender.Send(command);
    }

    public async Task<IResult> IndexLibraryFiles(ISender sender, Guid id)
    {
        await sender.Send(new IndexLibraryFilesCommand(id));
        return Results.NoContent();
    }

    public async Task<IResult> UpdateLibrary(ISender sender, Guid id, UpdateLibraryCommand command)
    {
        if (id != command.Id) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    public async Task<IResult> DeleteLibrary(ISender sender, Guid id)
    {
        await sender.Send(new DeleteLibraryCommand(id));
        return Results.NoContent();
    }
}
