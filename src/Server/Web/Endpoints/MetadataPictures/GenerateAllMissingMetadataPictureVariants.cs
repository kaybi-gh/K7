using K7.Server.Application.Features.MetadataPictures.Commands.GenerateAllMissingMetadataPictureVariants;
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
            var count = await sender.Send(new GenerateAllMissingMetadataPictureVariantsCommand(), cancellationToken);
            return Results.Ok(new { EnqueuedCount = count });
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
