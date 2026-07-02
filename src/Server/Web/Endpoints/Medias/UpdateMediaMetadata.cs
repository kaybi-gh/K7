using K7.Server.Application.Features.Medias.Commands.UpdateMediaMetadata;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class UpdateMediaMetadata : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/medias/{id}/metadata", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] UpdateMediaMetadataRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateMediaMetadataCommand
            {
                Id = id,
                LockedFields = request.LockedFields,
                Title = request.Title,
                SortTitle = request.SortTitle,
                OriginalTitle = request.OriginalTitle,
                ReleaseDate = request.ReleaseDate,
                Genres = request.Genres,
                Tagline = request.Tagline,
                Overview = request.Overview,
                OriginalLanguage = request.OriginalLanguage,
                ContentRating = request.ContentRating,
                Budget = request.Budget,
                Revenue = request.Revenue,
                Status = request.Status,
                Network = request.Network,
                AirDate = request.AirDate,
                Runtime = request.Runtime,
                Biography = request.Biography,
                Country = request.Country,
                TrackNumber = request.TrackNumber,
                DiscNumber = request.DiscNumber,
                Lyrics = request.Lyrics,
                LyricsLrc = request.LyricsLrc
            }, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

