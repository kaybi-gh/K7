using K7.Server.Application.Features.Metadata.Queries.SearchMetadata;
using K7.Server.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Metadata;

public class SearchMetadata : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/metadata/search", async ([FromServices] ISender sender, [FromQuery] string query, [FromQuery] int? year, [FromQuery] string? providerId, CancellationToken cancellationToken) =>  
        {
            var result = await sender.Send(new SearchMetadataQuery()
            {
                Query = query,
                Year = year,
                ProviderId = providerId
            }, cancellationToken);
            
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}