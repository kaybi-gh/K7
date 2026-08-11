using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Libraries.Commands.RematchLibraryMedia;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class RematchLibraryMedia : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/libraries/{id}/rematch-media", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new RematchLibraryMediaCommand(id),
                TargetEntityId = id,
                TargetEntityTypeName = nameof(Library),
                Lane = BackgroundTaskLane.LibraryScan,
                WorkClass = BackgroundTaskWorkClass.CriticalLink,
                TriggeredBy = BackgroundTaskTriggeredBy.User,
                MaxAttempts = 3,
                TimeoutSeconds = 3600
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
