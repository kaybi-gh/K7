using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.DynamicPlaylists.Queries.GetDynamicPlaylist;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities.Playlists;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.DynamicPlaylists;

public class GetDynamicPlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/dynamic-playlists/{id}", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            var dynamicPlaylist = await sender.Send(new GetDynamicPlaylistQuery(id), cancellationToken);
            return dynamicPlaylist.ToDynamicPlaylistDto();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
