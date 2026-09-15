using K7.Server.Application.Features.Collections.Commands.EvaluateCollection;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Collections;

public class EvaluateCollection : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/collections/{id}/evaluate", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken = default) =>
        {
            var result = await sender.Send(new EvaluateCollectionCommand
            {
                Id = id
            }, cancellationToken);

            return Results.Ok(new { Id = result });
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
