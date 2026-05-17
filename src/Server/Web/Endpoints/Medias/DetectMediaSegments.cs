using K7.Server.Application.Features.Medias.Commands.QueueDetectMediaSegments;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class DetectMediaSegments : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/medias/{id}/detect-segments", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new QueueDetectMediaSegmentsCommand { SeasonId = id }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
