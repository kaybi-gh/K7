using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Playlists.Queries.GetPlaylists;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities.Playlists;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Playlists;

public class GetPlaylists : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/playlists", async ([FromServices] ISender sender, [AsParameters] GetPlaylistsWithPaginationQuery query, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(query, cancellationToken);
            return new
            {
                result.PageNumber,
                result.TotalPages,
                result.TotalCount,
                Items = result.Items.Select(p => p.ToLitePlaylistDto())
            };
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
