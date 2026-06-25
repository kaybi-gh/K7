using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetIndexedFilePlaybackTracks;

public record IndexedFilePlaybackTracksDto(
    IReadOnlyList<AudioFileTrackDto> AudioTracks,
    IReadOnlyList<SubtitleFileTrackDto> SubtitleTracks);

public record GetIndexedFilePlaybackTracksQuery(Guid Id) : IRequest<IndexedFilePlaybackTracksDto>;

public class GetIndexedFilePlaybackTracksQueryHandler : IRequestHandler<GetIndexedFilePlaybackTracksQuery, IndexedFilePlaybackTracksDto>
{
    private readonly IApplicationDbContext _context;

    public GetIndexedFilePlaybackTracksQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IndexedFilePlaybackTracksDto> Handle(GetIndexedFilePlaybackTracksQuery request, CancellationToken cancellationToken)
    {
        var indexedFile = await _context.IndexedFiles
            .AsNoTracking()
            .Include(x => x.FileMetadata)
                .ThenInclude(x => (x as VideoFileMetadata)!.AudioTracks)
            .Include(x => x.FileMetadata)
                .ThenInclude(x => (x as VideoFileMetadata)!.SubtitleTracks)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, indexedFile);

        if (indexedFile.FileMetadata is not VideoFileMetadata videoMetadata)
        {
            return new IndexedFilePlaybackTracksDto([], []);
        }

        var audioTracks = videoMetadata.AudioTracks
            .OrderBy(t => t.Index)
            .Select(t => t.ToAudioFileTrackDto())
            .ToList();

        var subtitleTracks = videoMetadata.SubtitleTracks
            .OrderBy(t => t.Index)
            .Select(t => t.ToSubtitleFileTrackDto())
            .ToList();

        return new IndexedFilePlaybackTracksDto(audioTracks, subtitleTracks);
    }
}
