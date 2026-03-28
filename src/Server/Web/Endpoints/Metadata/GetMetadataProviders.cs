using K7.Server.Application.Features.Metadata.Queries.GetMetadataProviders;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Metadata;

public class GetMetadataProviders : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/metadata-providers", async (
            [FromQuery] LibraryMediaType? mediaType,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetMetadataProvidersQuery { MediaType = mediaType }, cancellationToken);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
