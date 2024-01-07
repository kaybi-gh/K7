using MediaServer.Application.Common.Models;
using MediaServer.Application.Libraries.Commands.CreateLibrary;
using MediaServer.Application.Libraries.Commands.DeleteLibrary;
using MediaServer.Application.Libraries.Commands.UpdateLibrary;
using MediaServer.Application.Libraries.Queries.GetLibraries;

namespace MediaServer.Web.Endpoints;

public class Libraries : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization()
            .MapGet(GetLibraries)
            .MapPost(CreateLibrary)
            .MapPut(UpdateLibrary, "{id}")
            .MapDelete(DeleteLibrary, "{id}");
    }

    public async Task<IEnumerable<LibraryDto>> GetLibraries(ISender sender)
    {
        return await sender.Send(new GetLibrariesQuery());
    }

    public async Task<int> CreateLibrary(ISender sender, CreateLibraryCommand command)
    {
        return await sender.Send(command);
    }

    public async Task<IResult> UpdateLibrary(ISender sender, int id, UpdateLibraryCommand command)
    {
        if (id != command.Id) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    public async Task<IResult> DeleteLibrary(ISender sender, int id)
    {
        await sender.Send(new DeleteLibraryCommand(id));
        return Results.NoContent();
    }
}
