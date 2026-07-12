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

            await using var stream = file.OpenReadStream();
            if (!await HasValidImageSignatureAsync(stream, cancellationToken))
                return Results.BadRequest("File must be a valid image.");

            await sender.Send(new UploadUserAvatarCommand
            {
                FileStream = stream,
                FileName = Path.ChangeExtension(file.FileName, Path.GetExtension(file.FileName).ToLowerInvariant())
            }, cancellationToken);

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

    private static async Task<bool> HasValidImageSignatureAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        stream.Position = 0;

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;

        if (read >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return true;

        if (read >= 6
            && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46
            && header[3] == 0x38 && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61)
            return true;

        if (read >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return true;

        return false;
    }
}
