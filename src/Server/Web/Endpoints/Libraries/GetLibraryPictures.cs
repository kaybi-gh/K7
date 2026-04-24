using K7.Server.Application.Features.Libraries.Queries.GetLibraryPictures;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class GetLibraryPictures : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/libraries/{id}/pictures", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetLibraryPicturesQuery(id), cancellationToken);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
