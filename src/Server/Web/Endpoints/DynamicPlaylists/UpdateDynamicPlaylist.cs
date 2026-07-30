using K7.Server.Application.Features.DynamicPlaylists.Commands.UpdateDynamicPlaylist;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.DynamicPlaylists;

public class UpdateDynamicPlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/dynamic-playlists/{id}", async ([FromServices] ISender sender, Guid id, [FromBody] UpdateDynamicPlaylistRequest request, CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateDynamicPlaylistCommand
            {
                Id = id,
                Title = request.Title,
                Description = request.Description,
                MediaType = request.MediaType,
                RuleFilter = request.RuleFilter,
                Limit = request.Limit,
                OrderBy = request.OrderBy,
                OrderDescending = request.OrderDescending
            }, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
