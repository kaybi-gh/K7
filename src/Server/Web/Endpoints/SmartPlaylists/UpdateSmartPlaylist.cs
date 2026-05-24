using K7.Server.Application.Features.SmartPlaylists.Commands.UpdateSmartPlaylist;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.SmartPlaylists;

public class UpdateSmartPlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/smart-playlists/{id}", async ([FromServices] ISender sender, Guid id, [FromBody] UpdateSmartPlaylistRequest request, CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateSmartPlaylistCommand
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
