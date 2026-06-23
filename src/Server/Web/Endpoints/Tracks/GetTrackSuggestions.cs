using K7.Server.Application.Features.MusicIntelligence.Queries.GetTrackSuggestions;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Tracks;

public class GetTrackSuggestions : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/tracks/suggestions", async (
            [FromQuery] string recentIds,
            [FromQuery] int count,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var trackIds = recentIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(Guid.Parse)
                .ToList();

            var result = await sender.Send(new GetTrackSuggestionsQuery(trackIds, count > 0 ? count : 20), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
