using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Playlists.Queries.GetPlaylistItems;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities.Playlists;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Playlists;

public class GetPlaylistItems : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/playlists/{playlistId}/items", async ([FromServices] ISender sender, Guid playlistId, int pageNumber = 1, int pageSize = PagingDefaults.DefaultPageSize, CancellationToken cancellationToken = default) =>
        {
            var result = await sender.Send(new GetPlaylistItemsWithPaginationQuery
            {
                PlaylistId = playlistId,
                PageNumber = pageNumber,
                PageSize = pageSize
            }, cancellationToken);

            return new
            {
                result.PageNumber,
                result.TotalPages,
                result.TotalCount,
                Items = result.Items.Select(i => i.ToPlaylistItemDto())
            };
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
