using K7.Server.Application.Features.Users.Commands.UpdateUserMediaExclusions;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UpdateUserMediaExclusions : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/{id:guid}/media-exclusions", async (
            [FromRoute] Guid id,
            [FromBody] UpdateUserMediaExclusionsRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateUserMediaExclusionsCommand
            {
                Id = id,
                ExcludedMediaIds = request.ExcludedMediaIds
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
