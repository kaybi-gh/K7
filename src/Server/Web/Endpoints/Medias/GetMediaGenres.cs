using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Medias.Queries.GetMediaGenres;
using K7.Server.Domain.Constants;
using K7.Server.Web.Converters;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class GetMediaGenres : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/medias/genres", async (
            [FromServices] ISender sender,
            [AsParameters] GetMediaGenresQuery query) =>
        {
            var result = await sender.Send(query);
            return Results.Ok(result.ToDto(x => x));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
