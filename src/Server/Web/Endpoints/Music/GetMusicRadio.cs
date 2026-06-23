using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.MusicRadio.Queries.GetMusicRadio;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
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
            [FromQuery] MusicRadioType radioType,
            [FromQuery] Guid[]? libraryIds,
            [FromQuery] Guid[]? libraryGroupIds,
            [FromQuery] Guid? seedTrackId,
            [FromQuery] Guid? seedArtistId,
            [FromQuery] string? moodPreset,
            [FromQuery] int? moodCentroidIndex,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default) =>
        {
            var tracks = await sender.Send(new GetMusicRadioQuery
            {
                RadioType = radioType,
                LibraryIds = libraryIds,
                LibraryGroupIds = libraryGroupIds,
                SeedTrackId = seedTrackId,
                SeedArtistId = seedArtistId,
                MoodPreset = moodPreset,
                MoodCentroidIndex = moodCentroidIndex,
                Limit = limit
            }, cancellationToken);

            return Results.Ok(tracks.Select(t => t.ToMediaDto()));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
