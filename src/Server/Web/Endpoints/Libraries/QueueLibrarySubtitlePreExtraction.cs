using K7.Server.Application.Features.Libraries.Commands.QueueLibrarySubtitlePreExtraction;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class QueueLibrarySubtitlePreExtraction : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/libraries/{id}/subtitle-pre-extraction", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            var queued = await sender.Send(new QueueLibrarySubtitlePreExtractionCommand(id), cancellationToken);
            return Results.Ok(queued);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
