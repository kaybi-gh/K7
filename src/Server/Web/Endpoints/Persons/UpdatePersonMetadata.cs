using K7.Server.Application.Features.Persons.Commands.UpdatePersonMetadata;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Persons;

public class UpdatePersonMetadata : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/persons/{id}/metadata", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] UpdatePersonMetadataRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdatePersonMetadataCommand
            {
                Id = id,
                LockedFields = request.LockedFields,
                Name = request.Name,
                Gender = request.Gender,
                Biography = request.Biography,
                Birthday = request.Birthday,
                Deathday = request.Deathday,
                BirthPlace = request.BirthPlace
            }, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

