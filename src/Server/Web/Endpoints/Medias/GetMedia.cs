using K7.Server.Application.Features.Medias.Queries.GetMedia;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class GetMedia : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/medias/{id}", async ([FromServices] ISender sender, Guid id) =>
        {
            var media = await sender.Send(new GetMediaQuery(id));
            return MediaDto.FromDomain(media);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
