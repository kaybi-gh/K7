using K7.Server.Application.Features.Persons.Commands.UploadPersonPicture;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Persons;

public class UploadPersonPicture : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/persons/{id}/pictures", async (
            HttpContext context,
            [FromServices] ISender sender,
            Guid id,
            [FromQuery] MetadataPictureType pictureType,
            CancellationToken cancellationToken) =>
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file");

            if (file is null)
                return Results.BadRequest("No file provided.");

            var stream = file.OpenReadStream();

            var pictureId = await sender.Send(new UploadPersonPictureCommand
            {
                PersonId = id,
                PictureType = pictureType,
                FileStream = stream,
                FileName = file.FileName
            }, cancellationToken);

            await stream.DisposeAsync();

            return Results.Ok(pictureId);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName)
        .DisableAntiforgery();
    }
}
