using K7.Server.Application.Features.Users.Commands.UploadUserAvatar;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UploadUserAvatar : IEndpoint
{
    private const long MaxFileSize = 2 * 1024 * 1024; // 2MB

    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/users/me/avatar", async (
            HttpContext context,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file");

            if (file is null)
                return Results.BadRequest("No file provided.");

            if (file.Length > MaxFileSize)
                return Results.BadRequest("File size exceeds 2MB limit.");

            if (!IsImageFile(file.ContentType, file.FileName))
                return Results.BadRequest("File must be an image.");

            var stream = file.OpenReadStream();

            await sender.Send(new UploadUserAvatarCommand
            {
                FileStream = stream,
                FileName = file.FileName
            }, cancellationToken);

            await stream.DisposeAsync();

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .DisableAntiforgery()
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static bool IsImageFile(string? contentType, string fileName)
    {
        if (contentType is not null && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return true;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp";
    }
}
