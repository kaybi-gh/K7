using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.MusicIntelligence.Queries.GetSimilarTracks;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetSimilarTracksQuery(Guid TrackId, int Count = 20) : IRequest<List<MusicIntelligenceTrackMatchDto>>;

public class GetSimilarTracksQueryHandler(
    IMusicIntelligenceService musicIntelligenceService,
    IApplicationDbContext context)
    : IRequestHandler<GetSimilarTracksQuery, List<MusicIntelligenceTrackMatchDto>>
{
    public async Task<List<MusicIntelligenceTrackMatchDto>> Handle(GetSimilarTracksQuery request, CancellationToken cancellationToken)
    {
        string? title = null;
        string? artist = null;

        var track = await context.Medias
            .AsNoTracking()
            .OfType<MusicTrack>()
            .Where(t => t.Id == request.TrackId)
            .Select(t => new
            {
                t.Title,
                Artist = t.Artist != null ? t.Artist.Title : t.Album.Artist != null ? t.Album.Artist.Title : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (track is not null)
        {
            title = track.Title;
            artist = track.Artist;
        }

        return await musicIntelligenceService.GetSimilarTracksAsync(
            request.TrackId,
            request.Count,
            title,
            artist,
            cancellationToken);
    }
}
