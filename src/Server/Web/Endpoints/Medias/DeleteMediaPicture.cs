using K7.Server.Application.Features.Medias.Commands.DeleteMediaPicture;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class DeleteMediaPicture : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/medias/{id}/pictures/{pictureId}", async (
            [FromServices] ISender sender,
            Guid id,
            Guid pictureId,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteMediaPictureCommand
            {
                MediaId = id,
                PictureId = pictureId
            }, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
