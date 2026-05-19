using K7.Server.Application.Features.Downloads.Commands.DeleteDownload;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Downloads;

public class DeleteDownload : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/downloads/{id:guid}", async (
            [FromServices] ISender sender,
            [FromRoute] Guid id,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteDownloadCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
