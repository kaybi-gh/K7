using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.Endpoints.FileSystem;

public class GetDirectories : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/filesystem/directories", (
            [FromQuery] string? path,
            [FromServices] IOptions<PathsConfiguration> pathsConfiguration,
            CancellationToken cancellationToken) =>
        {
            var allowedRoots = GetAllowedRoots(pathsConfiguration.Value);

            if (string.IsNullOrWhiteSpace(path))
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Select(d => new DirectoryEntryDto
                    {
                        Name = d.Name,
                        FullPath = d.RootDirectory.FullName
                    })
                    .ToList();

                return Results.Ok(new DirectoryContentDto
                {
                    Path = "",
                    Directories = drives
                });
            }

            var fullPath = Path.GetFullPath(path);

            if (!PathContainmentHelper.IsPathContained(fullPath, allowedRoots))
                return Results.Forbid();

            if (!Directory.Exists(fullPath))
            {
                return Results.NotFound(new { message = $"Directory not found: {path}" });
            }

            try
            {
                var directories = new DirectoryInfo(fullPath)
                    .EnumerateDirectories()
                    .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(d => new DirectoryEntryDto
                    {
                        Name = d.Name,
                        FullPath = d.FullName
                    })
                    .ToList();

                return Results.Ok(new DirectoryContentDto
                {
                    Path = fullPath,
                    Directories = directories
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static IEnumerable<string> GetAllowedRoots(PathsConfiguration paths) =>
    [
        paths.Config,
        paths.Metadatas,
        paths.Transcoding,
        paths.FFMpegBinaryFolder
    ];
}
