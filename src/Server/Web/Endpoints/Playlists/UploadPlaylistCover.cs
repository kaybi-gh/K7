using K7.Server.Application.Features.Playlists.Commands.RemovePlaylistCover;
using K7.Server.Application.Features.Playlists.Commands.UploadPlaylistCover;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Playlists;

public class UploadPlaylistCover : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/playlists/{id}/cover", async (
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

            var pictureId = await sender.Send(new UploadPlaylistCoverCommand
            {
                PlaylistId = id,
                FileStream = stream,
                FileName = fileName,
                SourcePictureId = sourcePictureId
            }, cancellationToken);

            if (stream is not null)
                await stream.DisposeAsync();

            return Results.Ok(pictureId);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .DisableAntiforgery()
        .WithName(type.Name)
        .WithTags(groupName);

        endpointRouteBuilder.MapDelete("/api/playlists/{id}/cover", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemovePlaylistCoverCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName($"{type.Name}Remove")
        .WithTags(groupName);
    }
}
