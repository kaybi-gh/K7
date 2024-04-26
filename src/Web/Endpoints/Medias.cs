using MediaServer.Application.Common.Models;
using MediaServer.Application.Features.Medias.Queries.GetMedia;
using MediaServer.Application.Features.Medias.Queries.GetMedias;

namespace MediaServer.Web.Endpoints;

public class Medias : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetMedia, "{id}")
            .MapGet(GetMedias);
    }

    public async Task<MediaDto> GetMedia(ISender sender, Guid id)
    {
        return await sender.Send(new GetMediaQuery(id));
    }

    public async Task<PaginatedList<LiteMediaDto>> GetMedias(ISender sender, [AsParameters] GetMediasWithPaginationQuery query)
    {
        return await sender.Send(query);
    }
}
