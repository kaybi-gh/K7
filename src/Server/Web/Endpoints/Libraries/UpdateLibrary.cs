using K7.Server.Application.Features.Libraries.Commands.UpdateLibrary;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class UpdateLibrary : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/libraries/{id}", async ([FromServices] ISender sender, Guid id, UpdateLibraryCommand command, CancellationToken cancellationToken) =>
        {
            await sender.Send(command with { Id = id }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
