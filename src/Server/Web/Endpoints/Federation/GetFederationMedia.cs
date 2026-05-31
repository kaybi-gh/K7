using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationMedia : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/libraries/{libraryId:guid}/media", async (
            Guid libraryId,
            [FromServices] IApplicationDbContext context,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            if (clientId is null)
                return Results.Forbid();

            var peer = await context.PeerServers
                .FirstOrDefaultAsync(p => p.InboundApplicationId == clientId && p.Status == PeerStatus.Active, cancellationToken);

            if (peer is null)
                return Results.Forbid();

            var isShared = await context.PeerShareAgreements
                .AnyAsync(a => a.PeerServerId == peer.Id
                    && a.LibraryId == libraryId
                    && a.Direction == ShareDirection.Outbound
                    && a.IsEnabled, cancellationToken);

            if (!isShared)
                return Results.Forbid();

            var library = await context.Libraries
                .FirstOrDefaultAsync(l => l.Id == libraryId && l.PeerServerId == null, cancellationToken);

            if (library is null)
                return Results.NotFound();

            // Expose top-level entities: Movies directly, Series/Albums via their children's files
            var directMedia = await context.Medias
                .Where(m => m.PeerServerId == null
                    && m is Movie
                    && m.IndexedFiles.Any(f => f.LibraryId == libraryId))
                .Include(m => m.ExternalIds)
                .Include(m => m.Pictures)
                .Include(m => m.IndexedFiles.Where(f => f.LibraryId == libraryId))
                    .ThenInclude(f => f.FileMetadata)
                        .ThenInclude(fm => (fm as VideoFileMetadata)!.AudioTracks)
                .Include(m => m.IndexedFiles.Where(f => f.LibraryId == libraryId))
                    .ThenInclude(f => f.FileMetadata)
                        .ThenInclude(fm => (fm as VideoFileMetadata)!.VideoTracks)
                .Include(m => m.IndexedFiles.Where(f => f.LibraryId == libraryId))
                    .ThenInclude(f => f.FileMetadata)
                        .ThenInclude(fm => (fm as VideoFileMetadata)!.SubtitleTracks)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var albums = await context.Medias
                .OfType<MusicAlbum>()
                .Where(a => a.PeerServerId == null
                    && a.Tracks.Any(t => t.IndexedFiles.Any(f => f.LibraryId == libraryId)))
                .Include(a => a.ExternalIds)
                .Include(a => a.Pictures)
                .Include(a => a.Tracks)
                    .ThenInclude(t => t.IndexedFiles.Where(f => f.LibraryId == libraryId))
                        .ThenInclude(f => f.FileMetadata)
                            .ThenInclude(fm => (fm as AudioFileMetadata)!.AudioTrack)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var series = await context.Medias
                .OfType<Serie>()
                .Where(s => s.PeerServerId == null
                    && s.Seasons.Any(ss => ss.Episodes.Any(e => e.IndexedFiles.Any(f => f.LibraryId == libraryId))))
                .Include(s => s.ExternalIds)
                .Include(s => s.Pictures)
                .Include(s => s.Seasons)
                    .ThenInclude(ss => ss.Episodes)
                        .ThenInclude(e => e.IndexedFiles.Where(f => f.LibraryId == libraryId))
                            .ThenInclude(f => f.FileMetadata)
                                .ThenInclude(fm => (fm as VideoFileMetadata)!.AudioTracks)
                .Include(s => s.Seasons)
                    .ThenInclude(ss => ss.Episodes)
                        .ThenInclude(e => e.IndexedFiles.Where(f => f.LibraryId == libraryId))
                            .ThenInclude(f => f.FileMetadata)
                                .ThenInclude(fm => (fm as VideoFileMetadata)!.VideoTracks)
                .Include(s => s.Seasons)
                    .ThenInclude(ss => ss.Episodes)
                        .ThenInclude(e => e.IndexedFiles.Where(f => f.LibraryId == libraryId))
                            .ThenInclude(f => f.FileMetadata)
                                .ThenInclude(fm => (fm as VideoFileMetadata)!.SubtitleTracks)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var mediaItems = new List<BaseMedia>();
            mediaItems.AddRange(directMedia);

            // For albums, flatten track files to the album level but preserve track ownership
            foreach (var album in albums)
            {
                mediaItems.Add(album);
            }

            // For series, flatten episode files to the serie level
            foreach (var serie in series)
            {
                serie.IndexedFiles = serie.Seasons
                    .SelectMany(s => s.Episodes)
                    .SelectMany(e => e.IndexedFiles)
                    .ToList();
                mediaItems.Add(serie);
            }

            var result = mediaItems.Select(m =>
            {
                var poster = m.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);
                var backdrop = m.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop);
                var logo = m.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Logo);

                var files = m is MusicAlbum albumMedia
                    ? albumMedia.Tracks.SelectMany(t => t.IndexedFiles.Select(f => BuildFileDto(f, t.Id))).ToList()
                    : m.IndexedFiles.Select(f => BuildFileDto(f, null)).ToList();

                return new PeerMediaDto
                {
                    Id = m.Id,
                    Type = m.Type,
                    Title = m.Title,
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
                    Genres = m.Genres.ToList()
                };
            }).ToList();

            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static PeerFileDto BuildFileDto(Domain.Entities.Metadatas.Files.IndexedFile f, Guid? mediaId)
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
