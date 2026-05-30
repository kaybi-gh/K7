using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Playlists.Queries.GetPlaylist;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities.Playlists;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Playlists;

public class GetPlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/playlists/{id}", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            var playlist = await sender.Send(new GetPlaylistQuery(id), cancellationToken);
            return playlist.ToPlaylistDto();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
