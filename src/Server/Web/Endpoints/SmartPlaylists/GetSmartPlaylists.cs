using K7.Server.Application.Features.SmartPlaylists.Queries.GetSmartPlaylists;
using K7.Shared.Dtos.Entities.Playlists;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.SmartPlaylists;

public class GetSmartPlaylists : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/smart-playlists", async ([FromServices] ISender sender, [AsParameters] GetSmartPlaylistsWithPaginationQuery query, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(query, cancellationToken);
            return new
            {
                result.PageNumber,
                result.TotalPages,
                result.TotalCount,
                Items = result.Items.Select(LiteSmartPlaylistDto.FromDomain)
            };
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
