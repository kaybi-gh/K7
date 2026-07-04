using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryPaths;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class NotifyIndexedFile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/indexed-files/notify", async (
            [FromBody] IndexedFileNotifyRequest body,
            [FromServices] IApplicationDbContext context,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var library = await context.Libraries
                .Where(l => l.RootPath != null && l.PeerServerId == null)
                .ToListAsync(cancellationToken);

            var matchingLibrary = library.FirstOrDefault(l =>
                body.Path.StartsWith(l.RootPath!, StringComparison.OrdinalIgnoreCase));

            if (matchingLibrary is null)
                return Results.NotFound("No library found for the given path");

            switch (body.Type)
            {
                case IndexedFileNotificationType.Added:
                case IndexedFileNotificationType.Modified:
                    await sender.Send(new CreateBackgroundTaskCommand
                    {
                        Request = new IndexLibraryPathsCommand(matchingLibrary.Id, [body.Path]),
                        Priority = BackgroundTaskPriority.Normal,
                        TargetEntityId = matchingLibrary.Id,
                        TargetEntityTypeName = nameof(Domain.Entities.Library),
                        MaxAttempts = 1,
                        TimeoutSeconds = 3600,
                        ConcurrencyGroup = "library-scan"
                    }, cancellationToken);
                    break;

                case IndexedFileNotificationType.Removed:
                    var indexedFile = await context.IndexedFiles
                        .FirstOrDefaultAsync(f => f.Path == body.Path, cancellationToken);

                    if (indexedFile is not null)
                    {
                        context.IndexedFiles.Remove(indexedFile);
                        await context.SaveChangesAsync(cancellationToken);
                    }
                    break;
            }

            return Results.Ok();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public sealed record IndexedFileNotifyRequest
{
    public required string Path { get; init; }
    public required IndexedFileNotificationType Type { get; init; }
}

public enum IndexedFileNotificationType
{
    Added = 1,
    Removed = 2,
    Modified = 3
}
