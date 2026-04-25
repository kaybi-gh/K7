using K7.Server.Application.Features.Collections.Commands.CreateCollection;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Collections;

public class CreateCollection : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/collections", async ([FromServices] ISender sender, CreateCollectionCommand command, CancellationToken cancellationToken) =>
        {
            return await sender.Send(command, cancellationToken);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
