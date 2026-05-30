using K7.Server.Application.Features.Libraries.Commands.UploadLibraryCover;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class UploadLibraryCover : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/library-groups/{id}/cover", async (
            HttpContext context,
            [FromServices] ISender sender,
            Guid id,
            [FromQuery] Guid? sourcePictureId,
            CancellationToken cancellationToken) =>
        {
            Stream? stream = null;
            string? fileName = null;

            if (sourcePictureId is null && context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                var file = form.Files.GetFile("file");
                if (file is not null)
                {
                    stream = file.OpenReadStream();
                    fileName = file.FileName;
                }
            }

            var pictureId = await sender.Send(new UploadLibraryCoverCommand
            {
                LibraryGroupId = id,
                FileStream = stream,
                FileName = fileName,
                SourcePictureId = sourcePictureId
            }, cancellationToken);

            if (stream is not null)
                await stream.DisposeAsync();

            return Results.Ok(pictureId);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .DisableAntiforgery()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
