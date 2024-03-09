using MediaServer.Application.Features.Libraries.Commands.CreateLibrary;
using MediaServer.Application.Features.Libraries.Commands.DeleteLibrary;
using MediaServer.Application.Features.Libraries.Commands.UpdateLibrary;
using MediaServer.Application.Features.Libraries.Queries.GetLibraries;

namespace MediaServer.Web.Endpoints;

public class Medias : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        /*app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetMe);*/
    }
}
