using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Features.Medias.Queries.GetNextEpisode;

public record GetNextEpisodeQuery(Guid SerieId, Guid CurrentEpisodeId) : IRequest<LiteSerieEpisodeDto?>;

public class GetNextEpisodeQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetNextEpisodeQuery, LiteSerieEpisodeDto?>
{
    public async Task<LiteSerieEpisodeDto?> Handle(GetNextEpisodeQuery request, CancellationToken cancellationToken)
    {
        var currentEpisode = await context.Medias
            .AsNoTracking()
            .OfType<SerieEpisode>()
            .Where(e => e.Id == request.CurrentEpisodeId && e.SerieId == request.SerieId)
            .Select(e => new { e.SeasonId, e.EpisodeNumber, e.Season.SeasonNumber })
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.CurrentEpisodeId, currentEpisode);

        Guid? userId = currentUser.Id;

        var nextInSeason = await BuildEpisodeQuery(userId)
            .Where(e => e.SeasonId == currentEpisode.SeasonId && e.EpisodeNumber > currentEpisode.EpisodeNumber)
            .OrderBy(e => e.EpisodeNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextInSeason is not null)
            return (LiteSerieEpisodeDto)nextInSeason.ToLiteMediaDto();

        var nextSeasonFirstEpisode = await BuildEpisodeQuery(userId)
            .Where(e => e.SerieId == request.SerieId
                && e.Season.SeasonNumber > currentEpisode.SeasonNumber)
            .OrderBy(e => e.Season.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextSeasonFirstEpisode is not null)
            return (LiteSerieEpisodeDto)nextSeasonFirstEpisode.ToLiteMediaDto();

        return null;
    }

    private IQueryable<SerieEpisode> BuildEpisodeQuery(Guid? userId)
    {
        var query = context.Medias
            .AsNoTracking()
            .OfType<SerieEpisode>()
            .Include(e => e.Pictures)
                .ThenInclude(p => p.Variants)
            .Include(e => e.IndexedFiles)
                .ThenInclude(f => f.FileMetadata)
            .Include(e => e.Season)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Include(e => e.UserMediaStates.Where(s => s.UserId == userId.Value));
        }

        return query;
    }
}
