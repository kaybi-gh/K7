using K7.Server.Application.Features.Metadata.Queries.SearchMetadata;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Metadata;

public class SearchMetadata : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/metadata/search", async ([FromServices] ISender sender, [FromQuery] string query, [FromQuery] int? year, [FromQuery] string? providerId, [FromQuery] MediaType? mediaType, [FromQuery] string? language, HttpContext httpContext, CancellationToken cancellationToken) =>  
        {
            var resolvedLanguage = language
                ?? httpContext.Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',').FirstOrDefault()?.Split('-').FirstOrDefault()
                ?? "en";

            var result = await sender.Send(new SearchMetadataQuery()
            {
                Query = query,
                Year = year,
                ProviderId = providerId,
                MediaType = mediaType,
                Language = resolvedLanguage
            }, cancellationToken);
            
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}