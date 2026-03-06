using K7.Server.Application.Features.Medias.Commands.ReidentifyMedia;
using K7.Shared.Dtos.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class ReidentifyMedia : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/medias/{id}/reidentify", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] ReidentifyMediaRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new ReidentifyMediaCommand
            {
                MediaId = id,
                SelectedProvider = request.SelectedProvider,
                SelectedExternalId = request.SelectedExternalId
            }, cancellationToken);

            return Results.NoContent();
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}