using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Collections.Queries.GetCollection;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Collections;

public class GetCollection : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/collections/{id}", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            var collection = await sender.Send(new GetCollectionQuery(id), cancellationToken);
            return collection.ToCollectionDto();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
