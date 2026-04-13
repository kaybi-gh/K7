using K7.Server.Application.Features.IndexedFiles.Queries.GetDirectStream;
using K7.Server.Domain.Constants;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetDirectStream : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods(GetIndexedFileDirectStreamQueryUriBuilder.Route, ["GET", "HEAD"], async ([FromServices] ISender sender, [FromRoute] Guid id) =>
        {
            return await sender.Send(new GetDirectStreamQuery(id));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
