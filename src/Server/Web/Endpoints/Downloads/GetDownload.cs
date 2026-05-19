using K7.Server.Application.Features.Downloads.Queries.GetDownload;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Downloads;

public class GetDownload : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/downloads/{id:guid}", async (
            [FromServices] ISender sender,
            [FromRoute] Guid id,
            CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetDownloadQuery(id), cancellationToken);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
