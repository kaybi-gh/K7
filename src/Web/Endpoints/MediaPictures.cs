using MediaServer.Application.Features.Libraries.Queries.GetMediaPicture;
using Microsoft.AspNetCore.Mvc;

namespace MediaServer.Web.Endpoints;

public class Pictures : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetMediaPicture, "{id}");
    }

    public async Task<IResult> GetMediaPicture(ISender sender, [FromRoute] int id)
    {
        return await sender.Send(new GetMediaPictureQuery(id));
    }
}
