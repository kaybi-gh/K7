using K7.Server.Application.Features.Medias.Commands.ImportMediaPictureFromUrl;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class ImportMediaPicture : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/medias/{id}/pictures/import", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] ImportMediaPictureFromUrlRequest request,
            CancellationToken cancellationToken) =>
        {
            var pictureId = await sender.Send(new ImportMediaPictureFromUrlCommand
            {
                MediaId = id,
                Url = request.Url,
                PictureType = request.PictureType
            }, cancellationToken);

            return Results.Ok(pictureId);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
