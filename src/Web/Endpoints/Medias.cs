using MediaServer.Application.Common.Models;
using MediaServer.Application.Features.Medias.Queries.GetMedias;
using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Web.Endpoints;

public class Medias : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetMedias);
    }

    public async Task<PaginatedList<BaseMedia>> GetMedias(ISender sender, [AsParameters] GetMediasWithPaginationQuery query)
    {
        return await sender.Send(query);
    }
}
