using K7.Server.Application.Features.MusicIntelligence.Queries.SearchTracksBySonicText;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.MusicIntelligence;

public class SearchTracksBySonicText : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/music-intelligence/search/sonic", async (
            [FromBody] MusicIntelligenceSearchRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new SearchTracksBySonicTextQuery
            {
                Query = request.Query,
                Count = request.Count
            }, cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
