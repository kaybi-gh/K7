using K7.Server.Application.Features.Collections.Commands.UpdateCollection;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Collections;

public class UpdateCollection : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/collections/{id}", async ([FromServices] ISender sender, Guid id, UpdateCollectionCommand command, CancellationToken cancellationToken) =>
        {
            if (id != command.Id) return Results.BadRequest();
            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
