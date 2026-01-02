using K7.Server.Application.Features.MediaFormats.Queries.GetMediaFormats;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class GetMediaFormats : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/media-formats", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
        {
            var mediaFormats = await sender.Send(new GetMediaFormatsQuery(), cancellationToken);
            return mediaFormats.Select(MediaFormatDto.FromDomain);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
