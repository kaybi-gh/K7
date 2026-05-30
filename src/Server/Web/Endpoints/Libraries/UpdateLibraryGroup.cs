using K7.Server.Application.Features.LibraryGroups.Commands.UpdateLibraryGroup;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class UpdateLibraryGroup : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/library-groups/{id}", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] UpdateLibraryGroupRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateLibraryGroupCommand
            {
                Id = id,
                Title = request.Title,
                Description = request.Description,
                Icon = request.Icon
            }, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
