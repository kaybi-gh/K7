using K7.Server.Application.Features.Libraries.Commands.UpdateLibrary;
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
            if (id != command.Id)
            {
                return Results.BadRequest();
            }

            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
