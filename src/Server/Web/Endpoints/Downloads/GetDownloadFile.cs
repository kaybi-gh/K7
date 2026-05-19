using K7.Server.Application.Features.Downloads.Queries.GetDownloadFile;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Downloads;

public class GetDownloadFile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/api/downloads/{id:guid}/file", ["GET", "HEAD"], async (
            [FromServices] ISender sender,
            [FromRoute] Guid id) =>
        {
            return await sender.Send(new GetDownloadFileQuery(id));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
