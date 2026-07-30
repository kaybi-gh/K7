using K7.Server.Application.Features.DynamicPlaylists.Commands.CreateDynamicPlaylist;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.DynamicPlaylists;

public class CreateDynamicPlaylist : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/dynamic-playlists", async ([FromServices] ISender sender, [FromBody] CreateDynamicPlaylistRequest request, CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new CreateDynamicPlaylistCommand
            {
                Title = request.Title,
                Description = request.Description,
                MediaType = request.MediaType,
                RuleFilter = request.RuleFilter,
                Limit = request.Limit,
                OrderBy = request.OrderBy,
                OrderDescending = request.OrderDescending
            }, cancellationToken);

            return Results.Created($"/api/dynamic-playlists/{id}", id);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
