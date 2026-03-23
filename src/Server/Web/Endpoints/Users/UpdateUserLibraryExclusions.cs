using K7.Server.Application.Features.Users.Commands.UpdateUserLibraryExclusions;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UpdateUserLibraryExclusions : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/{id:guid}/library-exclusions", async (
            [FromRoute] Guid id,
            [FromBody] UpdateUserLibraryExclusionsRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateUserLibraryExclusionsCommand
            {
                Id = id,
                ExcludedLibraryIds = request.ExcludedLibraryIds
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
