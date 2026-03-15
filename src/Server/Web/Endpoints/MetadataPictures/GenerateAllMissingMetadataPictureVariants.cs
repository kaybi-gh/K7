using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateAllMissingMetadataPictureVariants;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.MetadataPictures;

public class GenerateAllMissingMetadataPictureVariants : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/metadata-pictures/generate-missing-variants", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new GenerateAllMissingMetadataPictureVariantsCommand(),
                Priority = BackgroundTaskPriority.Low,
            }, cancellationToken);
            return Results.Accepted();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
