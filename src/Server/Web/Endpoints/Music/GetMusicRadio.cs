using K7.Server.Application.Features.MusicRadio.Queries.GetMusicRadio;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Music;

public class GetMusicRadio : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/music/radio", async (
            [FromServices] ISender sender,
            [AsParameters] GetMusicRadioQuery query,
            CancellationToken cancellationToken) =>
        {
            var tracks = await sender.Send(query, cancellationToken);
            return tracks.Select(MediaDto.FromDomain);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
