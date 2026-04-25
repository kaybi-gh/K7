using K7.Server.Application.Features.Search.Queries.GlobalSearch;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Search;

public class GlobalSearch : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/search", async ([FromServices] ISender sender, [AsParameters] GlobalSearchQuery query, CancellationToken cancellationToken) =>
        {
            return await sender.Send(query, cancellationToken);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
