using K7.Server.Application.Features.SmartPlaylists.Queries.GetSmartPlaylist;
using K7.Shared.Dtos.Entities.Playlists;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.SmartPlaylists;

public class GetSmartPlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/smart-playlists/{id}", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            var smartPlaylist = await sender.Send(new GetSmartPlaylistQuery(id), cancellationToken);
            return SmartPlaylistDto.FromDomain(smartPlaylist);
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
