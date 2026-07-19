using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.FileSystem;

public class GetDirectories : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/filesystem/directories", (
            [FromQuery] string? path,
            CancellationToken cancellationToken) =>
        {
            var allowedRoots = GetBrowsableRoots();

            if (string.IsNullOrWhiteSpace(path))
            {
                var drives = allowedRoots
                    .Select(root => new DirectoryEntryDto
                    {
                        Name = root,
                        FullPath = root
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
            {
                return Results.Problem(
                    detail: "Path is outside allowed browse roots.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (!Directory.Exists(fullPath))
            {
                return Results.Problem(
                    detail: $"Directory not found: {path}",
                    statusCode: StatusCodes.Status404NotFound);
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
                return Results.Problem(
                    detail: "Access to this directory is denied.",
                    statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static IReadOnlyList<string> GetBrowsableRoots() =>
        DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => d.RootDirectory.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
