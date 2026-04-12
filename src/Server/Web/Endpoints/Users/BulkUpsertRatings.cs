using K7.Server.Application.Features.Users.Commands.BulkUpsertRatings;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class BulkUpsertRatings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/users/{userId:guid}/ratings/bulk", async (
            [FromRoute] Guid userId,
            [FromBody] BulkUpsertRatingsRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var count = await sender.Send(new BulkUpsertRatingsCommand
            {
                UserId = userId,
                Items = request.Items,
                Strategy = request.Strategy
            }, cancellationToken);
            return Results.Ok(new { UpsertedCount = count });
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
