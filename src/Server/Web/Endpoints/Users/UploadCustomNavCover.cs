using K7.Server.Application.Features.CustomNav.Commands.UploadCustomNavCover;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UploadCustomNavCover : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/users/me/preferences/custom-nav/covers", async (
            HttpContext context,
            [FromServices] ISender sender,
            [FromQuery] Guid itemId,
            [FromQuery] Guid? sourcePictureId,
            [FromQuery] Guid? replacePictureId,
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

            var pictureId = await sender.Send(new UploadCustomNavCoverCommand
            {
                ItemId = itemId,
                FileStream = stream,
                FileName = fileName,
                SourcePictureId = sourcePictureId,
                ReplacePictureId = replacePictureId
            }, cancellationToken);

            if (stream is not null)
                await stream.DisposeAsync();

            return Results.Ok(pictureId);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .DisableAntiforgery()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
