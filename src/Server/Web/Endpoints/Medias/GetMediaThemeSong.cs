using K7.Server.Application.Features.Medias.Queries.GetMediaThemeSong;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class GetMediaThemeSong : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/medias/{id}/theme", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetMediaThemeSongQuery(id), cancellationToken);
            if (result is null)
                return Results.NotFound();

            return Results.File(result.FilePath, result.ContentType, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
