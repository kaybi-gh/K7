using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;

namespace K7.Server.Application.Features.Medias.Queries.GetMedia;

public record GetMediaQuery(Guid Id) : IRequest<BaseMedia>;

public class GetMediaQueryHandler(IApplicationDbContext context, IUser currentUser, IMediaAccessGuard accessGuard)
    : IRequestHandler<GetMediaQuery, BaseMedia>
{
    public async Task<BaseMedia> Handle(GetMediaQuery request, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(request.Id, cancellationToken);

        Guid? userId = currentUser.Id;

        var query = context.Medias
            .AsNoTracking()
            .Include(x => x.ExternalIds)
            .Include(x => x.Pictures)
                .ThenInclude(p => p.Variants)
            .Include(x => x.Ratings)
            .Include(x => x.PersonRoles)
                .ThenInclude(x => x.PortraitPicture)
            .Include(x => x.PersonRoles)
                .ThenInclude(x => x.Person)
                    .ThenInclude(x => x.PortraitPicture)
            .Include(x => x.IndexedFiles)
                .ThenInclude(x => x.FileMetadata)
                    .ThenInclude(x => (x as AudioFileMetadata)!.AudioTrack)
            .Include(x => x.IndexedFiles)
                .ThenInclude(x => x.FileMetadata)
                    .ThenInclude(x => (x as VideoFileMetadata)!.AudioTracks)
            .Include(x => x.IndexedFiles)
                .ThenInclude(x => x.FileMetadata)
                    .ThenInclude(x => (x as VideoFileMetadata)!.SubtitleTracks)
            .Include(x => x.IndexedFiles)
                .ThenInclude(x => x.FileMetadata)
                    .ThenInclude(x => (x as VideoFileMetadata)!.VideoTracks)
            .Include(x => x.IndexedFiles)
                .ThenInclude(x => x.FileMetadata)
                    .ThenInclude(x => (x as VideoFileMetadata)!.Thumbnails)
            .Include(x => (x as MusicAlbum)!.Tracks)
                .ThenInclude(t => t.Pictures)
                    .ThenInclude(p => p.Variants)
            .Include(x => (x as MusicAlbum)!.Tracks)
                .ThenInclude(t => t.IndexedFiles)
                    .ThenInclude(f => f.FileMetadata)
            .Include(x => (x as MusicAlbum)!.Tracks)
                .ThenInclude(t => t.AudioAnalysis)
            .Include(x => (x as MusicTrack)!.AudioAnalysis)
            // Serie: include seasons with their pictures and episode counts
            .Include(x => (x as Serie)!.Seasons)
                .ThenInclude(s => s.Pictures)
                    .ThenInclude(p => p.Variants)
            .Include(x => (x as Serie)!.Seasons)
                .ThenInclude(s => s.Episodes)
            // SerieSeason: include episodes with their pictures, indexed files, and user states
            .Include(x => (x as SerieSeason)!.Episodes)
                .ThenInclude(e => e.Pictures)
                    .ThenInclude(p => p.Variants)
            .Include(x => (x as SerieSeason)!.Episodes)
                .ThenInclude(e => e.IndexedFiles)
                    .ThenInclude(f => f.FileMetadata)
            .Include(x => (x as SerieSeason)!.Serie)
            // SerieEpisode: include serie and season for context
            .Include(x => (x as SerieEpisode)!.Serie)
            .Include(x => (x as SerieEpisode)!.Season)
            // MusicArtist: include albums with tracks
            .Include(x => (x as MusicArtist)!.Albums)
                .ThenInclude(a => a.Tracks)
                    .ThenInclude(t => t.Pictures)
                        .ThenInclude(p => p.Variants)
            .Include(x => (x as MusicArtist)!.Albums)
                .ThenInclude(a => a.Tracks)
                    .ThenInclude(t => t.IndexedFiles)
            .Include(x => (x as MusicArtist)!.Albums)
                .ThenInclude(a => a.Pictures)
                    .ThenInclude(p => p.Variants)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query
                .Include(x => x.UserMediaStates.Where(s => s.UserId == userId.Value))
                .Include(x => (x as SerieSeason)!.Episodes)
                    .ThenInclude(e => e.UserMediaStates.Where(s => s.UserId == userId.Value));
        }

        var entity = await query
            .Where(x => x.Id == request.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return entity;
    }
}
