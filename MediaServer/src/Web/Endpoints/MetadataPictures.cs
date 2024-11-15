using MediaServer.Application.Features.MetadataPictures.Queries.GetMetadataPicture;
using Microsoft.AspNetCore.Mvc;

namespace MediaServer.Web.Endpoints;

public class MetadataPictures : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetMetadataPicture, "{id}");
    }

    public async Task<IResult> GetMetadataPicture(ISender sender, [FromRoute] Guid id)
    {
        return await sender.Send(new GetMetadataPictureQuery(id));
    }
}
