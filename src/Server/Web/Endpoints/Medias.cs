using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Models.Dtos;
using K7.Server.Application.Features.Medias.Queries.GetMedia;
using K7.Server.Application.Features.Medias.Queries.GetMedias;

namespace K7.Server.Web.Endpoints;

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
