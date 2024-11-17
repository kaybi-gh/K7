using K7.Server.Application.Features.MetadataPictures.Queries.GetMetadataPicture;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints;

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
