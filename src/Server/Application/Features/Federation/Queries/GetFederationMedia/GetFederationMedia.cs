using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationMedia;

public record GetFederationMediaQuery(string? ClientId, Guid LibraryId) : IRequest<IReadOnlyList<PeerMediaDto>>;

public class GetFederationMediaQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context)
    : IRequestHandler<GetFederationMediaQuery, IReadOnlyList<PeerMediaDto>>
{
    public async Task<IReadOnlyList<PeerMediaDto>> Handle(
        GetFederationMediaQuery request,
        CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(request.ClientId, cancellationToken);
        await peerAuthorization.RequireLibrarySharedWithPeerAsync(peer.Id, request.LibraryId, cancellationToken);

        var library = await context.Libraries
            .FirstOrDefaultAsync(l => l.Id == request.LibraryId && l.PeerServerId == null, cancellationToken);

        if (library is null)
            throw new NotFoundException(request.LibraryId.ToString(), "Library");

        var directMedia = await context.Medias
            .Where(m => m.PeerServerId == null && m is Movie)
            .WhereLinkedToLibrary(context, request.LibraryId)
            .Include(m => m.ExternalIds)
            .Include(m => m.Pictures)
            .Include(m => m.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(m => m.IndexedFiles.Where(f => f.LibraryId == request.LibraryId))
                .ThenInclude(f => f.FileMetadata)
                    .ThenInclude(fm => (fm as VideoFileMetadata)!.AudioTracks)
            .Include(m => m.IndexedFiles.Where(f => f.LibraryId == request.LibraryId))
                .ThenInclude(f => f.FileMetadata)
                    .ThenInclude(fm => (fm as VideoFileMetadata)!.VideoTracks)
            .Include(m => m.IndexedFiles.Where(f => f.LibraryId == request.LibraryId))
                .ThenInclude(f => f.FileMetadata)
                    .ThenInclude(fm => (fm as VideoFileMetadata)!.SubtitleTracks)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var albums = await context.Medias
            .OfType<MusicAlbum>()
            .Where(a => a.PeerServerId == null)
            .WhereLinkedToLibrary(context, request.LibraryId)
            .Include(a => a.ExternalIds)
            .Include(a => a.Pictures)
            .Include(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(a => a.Tracks)
                .ThenInclude(t => t.IndexedFiles.Where(f => f.LibraryId == request.LibraryId))
                    .ThenInclude(f => f.FileMetadata)
                        .ThenInclude(fm => (fm as AudioFileMetadata)!.AudioTrack)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var series = await context.Medias
            .OfType<Serie>()
            .Where(s => s.PeerServerId == null)
            .WhereLinkedToLibrary(context, request.LibraryId)
            .Include(s => s.ExternalIds)
            .Include(s => s.Pictures)
            .Include(s => s.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(s => s.Seasons)
                .ThenInclude(ss => ss.Episodes)
                    .ThenInclude(e => e.IndexedFiles.Where(f => f.LibraryId == request.LibraryId))
                        .ThenInclude(f => f.FileMetadata)
                            .ThenInclude(fm => (fm as VideoFileMetadata)!.AudioTracks)
            .Include(s => s.Seasons)
                .ThenInclude(ss => ss.Episodes)
                    .ThenInclude(e => e.IndexedFiles.Where(f => f.LibraryId == request.LibraryId))
                        .ThenInclude(f => f.FileMetadata)
                            .ThenInclude(fm => (fm as VideoFileMetadata)!.VideoTracks)
            .Include(s => s.Seasons)
                .ThenInclude(ss => ss.Episodes)
                    .ThenInclude(e => e.IndexedFiles.Where(f => f.LibraryId == request.LibraryId))
                        .ThenInclude(f => f.FileMetadata)
                            .ThenInclude(fm => (fm as VideoFileMetadata)!.SubtitleTracks)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var mediaItems = new List<BaseMedia>();
        mediaItems.AddRange(directMedia);
        mediaItems.AddRange(albums);
        mediaItems.AddRange(series);

        return mediaItems.Select(m =>
        {
            var poster = m.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);
            var backdrop = m.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop);
            var logo = m.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Logo);

            var files = m is MusicAlbum albumMedia
                ? albumMedia.Tracks.SelectMany(t => t.IndexedFiles.Select(f => FederationPeerFileMapper.BuildFileDto(f, t.Id))).ToList()
                : m is Serie serieMedia
                    ? serieMedia.Seasons.SelectMany(s => s.Episodes).SelectMany(e => e.IndexedFiles.Select(f => FederationPeerFileMapper.BuildFileDto(f, e.Id))).ToList()
                    : m.IndexedFiles.Select(f => FederationPeerFileMapper.BuildFileDto(f, null)).ToList();

            return new PeerMediaDto
            {
                Id = m.Id,
                Type = m.Type,
                Title = m.Title,
                SortTitle = m.SortTitle,
                OriginalTitle = m.OriginalTitle,
                ReleaseDate = m.ReleaseDate,
                Overview = (m as Movie)?.Overview ?? (m as Serie)?.Overview,
                Tagline = (m as Movie)?.Tagline,
                OriginalLanguage = (m as Movie)?.OriginalLanguage ?? (m as Serie)?.OriginalLanguage,
                PosterPictureId = poster?.Id,
                BackdropPictureId = backdrop?.Id,
                LogoPictureId = logo?.Id,
                ExternalIds = m.ExternalIds.Select(e => new PeerExternalIdDto
                {
                    Provider = e.ProviderName,
                    Value = e.Value
                }).ToList(),
                Files = files,
                Genres = m.MetadataTags
                    .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                    .Select(mt => mt.MetadataTag.DisplayName)
                    .ToList()
            };
        }).ToList();
    }
}

internal static class FederationPeerFileMapper
{
    internal static PeerFileDto BuildFileDto(Domain.Entities.IndexedFile f, Guid? mediaId)
    {
        var vMeta = f.FileMetadata as VideoFileMetadata;
        var aMeta = f.FileMetadata as AudioFileMetadata;
        return new PeerFileDto
        {
            Id = f.Id,
            MediaId = mediaId,
            Name = f.Name,
            Extension = f.Extension,
            Size = f.Size,
            Container = f.FileMetadata?.Container,
            Duration = vMeta?.Duration ?? aMeta?.Duration,
            VideoBitrate = vMeta?.VideoBitrate,
            VideoResolution = vMeta?.VideoResolution,
            AudioTracks = vMeta?.AudioTracks.Select(t => new PeerAudioTrackDto
            {
                Index = t.Index,
                IsDefault = t.IsDefault,
                Name = t.Name,
                Language = t.Language,
                Codec = t.Codec,
                Channels = t.Channels,
                ChannelLayout = t.ChannelLayout,
                SampleRateHz = t.SampleRateHz,
                Profile = t.Profile
            }).ToList() ?? [],
            VideoTracks = vMeta?.VideoTracks.Select(t => new PeerVideoTrackDto
            {
                Index = t.Index,
                IsDefault = t.IsDefault,
                Width = t.Width,
                Height = t.Height,
                Codec = t.Codec,
                Profile = t.Profile,
                Level = t.Level,
                PixelFormat = t.PixelFormat,
                BitDepth = t.BitDepth
            }).ToList() ?? [],
            SubtitleTracks = vMeta?.SubtitleTracks.Select(t => new PeerSubtitleTrackDto
            {
                Index = t.Index,
                IsDefault = t.IsDefault,
                Name = t.Name,
                Language = t.Language,
                Codec = t.Codec,
                IsTextBased = t.IsTextBased,
                IsForced = t.IsForced,
                IsHearingImpaired = t.IsHearingImpaired
            }).ToList() ?? []
        };
    }
}
