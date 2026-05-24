using K7.Server.Application.Features.SmartPlaylists.Commands.CreateSmartPlaylist;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.SmartPlaylists;

public class CreateSmartPlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/smart-playlists", async ([FromServices] ISender sender, [FromBody] CreateSmartPlaylistRequest request, CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new CreateSmartPlaylistCommand
            {
                Title = request.Title,
                Description = request.Description,
                MediaType = request.MediaType,
                RuleFilter = request.RuleFilter,
                Limit = request.Limit,
                OrderBy = request.OrderBy,
                OrderDescending = request.OrderDescending
            }, cancellationToken);

            return Results.Created($"/api/smart-playlists/{id}", id);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
