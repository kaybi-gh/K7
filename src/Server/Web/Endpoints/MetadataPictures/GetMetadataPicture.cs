using K7.Server.Application.Features.MetadataPictures.Queries.GetMetadataPicture;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.MetadataPictures;

public class GetMetadataPicture : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/metadata-pictures/{id}", async (
            [FromServices] ISender sender,
            Guid id,
            [FromQuery] MetadataPictureSize? size) =>
        {
            return await sender.Send(new GetMetadataPictureQuery(id, size));
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
