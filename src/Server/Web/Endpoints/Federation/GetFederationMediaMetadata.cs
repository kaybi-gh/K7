using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationMediaMetadata : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/media/{mediaId:guid}/metadata", async (
            Guid mediaId,
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

            var media = await context.Medias
                .Include(m => m.ExternalIds)
                .Include(m => m.Pictures)
                .Include(m => m.MetadataTags).ThenInclude(mt => mt.MetadataTag)
                .Include(m => m.Trailers)
                .Include(m => m.Ratings)
                .Include(m => m.PersonRoles)
                    .ThenInclude(r => r.Person)
                        .ThenInclude(p => p.ExternalIds)
                .Include(m => m.PersonRoles)
                    .ThenInclude(r => r.Person)
                        .ThenInclude(p => p.PortraitPicture)
                .Include(m => m.PersonRoles)
                    .ThenInclude(r => r.ExternalIds)
                .Include(m => m.PersonRoles)
                    .ThenInclude(r => r.PortraitPicture)
                .Include(m => ((MusicAlbum)m).Tracks)
                    .ThenInclude(t => t.ExternalIds)
                .Include(m => ((MusicAlbum)m).Tracks)
                    .ThenInclude(t => t.IndexedFiles)
                        .ThenInclude(f => f.FileMetadata)
                .Include(m => ((MusicAlbum)m).Tracks)
                    .ThenInclude(t => t.ArtistCredits)
                        .ThenInclude(c => c.MusicArtist)
                            .ThenInclude(a => a.ExternalIds)
                .Include(m => ((MusicAlbum)m).Artist)
                    .ThenInclude(a => a!.ExternalIds)
                .Include(m => ((Serie)m).Seasons)
                    .ThenInclude(s => s.Episodes)
                        .ThenInclude(e => e.ExternalIds)
                .Include(m => ((Serie)m).Seasons)
                    .ThenInclude(s => s.Episodes)
                        .ThenInclude(e => e.Pictures)
                .Include(m => ((Serie)m).Seasons)
                    .ThenInclude(s => s.Episodes)
                        .ThenInclude(e => e.PersonRoles)
                            .ThenInclude(r => r.Person)
                                .ThenInclude(p => p.ExternalIds)
                .Include(m => ((Serie)m).Seasons)
                    .ThenInclude(s => s.Episodes)
                        .ThenInclude(e => e.PersonRoles)
                            .ThenInclude(r => r.Person)
                                .ThenInclude(p => p.PortraitPicture)
                .Include(m => ((Serie)m).Seasons)
                    .ThenInclude(s => s.Episodes)
                        .ThenInclude(e => e.PersonRoles)
                            .ThenInclude(r => r.ExternalIds)
                .Include(m => ((Serie)m).Seasons)
                    .ThenInclude(s => s.Episodes)
                        .ThenInclude(e => e.PersonRoles)
                            .ThenInclude(r => r.PortraitPicture)
                .Include(m => ((Serie)m).Seasons)
                    .ThenInclude(s => s.ExternalIds)
                .Include(m => ((Serie)m).Seasons)
                    .ThenInclude(s => s.Pictures)
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.Id == mediaId && m.PeerServerId == null, cancellationToken);

            if (media is null)
                return Results.NotFound();

            // For parent entities (MusicAlbum, Serie), files belong to child media (tracks, episodes).
            // Check share based on entity type to avoid untranslatable null checks in LINQ.
            bool isShared;
            if (media is MusicAlbum)
            {
                isShared = await context.PeerShareAgreements
                    .AnyAsync(a => a.PeerServerId == peer.Id
                        && a.Direction == ShareDirection.Outbound
                        && a.IsEnabled
                        && context.IndexedFiles.Any(f => f.LibraryId == a.LibraryId
                            && (f.MediaId == mediaId
                                || context.Medias.OfType<MusicTrack>()
                                    .Where(t => t.AlbumId == mediaId)
                                    .Select(t => (Guid?)t.Id)
                                    .Contains(f.MediaId))), cancellationToken);
            }
            else if (media is Serie)
            {
                isShared = await context.PeerShareAgreements
                    .AnyAsync(a => a.PeerServerId == peer.Id
                        && a.Direction == ShareDirection.Outbound
                        && a.IsEnabled
                        && context.IndexedFiles.Any(f => f.LibraryId == a.LibraryId
                            && (f.MediaId == mediaId
                                || context.Medias.OfType<SerieEpisode>()
                                    .Where(e => e.SerieId == mediaId)
                                    .Select(e => (Guid?)e.Id)
                                    .Contains(f.MediaId))), cancellationToken);
            }
            else
            {
                isShared = await context.PeerShareAgreements
                    .AnyAsync(a => a.PeerServerId == peer.Id
                        && a.Direction == ShareDirection.Outbound
                        && a.IsEnabled
                        && context.IndexedFiles.Any(f => f.LibraryId == a.LibraryId
                            && f.MediaId == mediaId), cancellationToken);
            }

            if (!isShared)
                return Results.Forbid();

            var dto = BuildDto(media);

            return Results.Ok(dto);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static PeerFullMediaMetadataDto BuildDto(BaseMedia media)
    {
        var genres = media.MetadataTags
            .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
            .Select(mt => mt.MetadataTag.DisplayName)
            .ToList();

        var dto = new PeerFullMediaMetadataDto
        {
            Id = media.Id,
            Type = media.Type,
            Title = media.Title ?? string.Empty,
            SortTitle = media.SortTitle,
            OriginalTitle = media.OriginalTitle,
            ReleaseDate = media.ReleaseDate,
            Genres = genres,
            ExternalIds = media.ExternalIds.Select(e => new PeerExternalIdDto
            {
                Provider = e.ProviderName,
                Value = e.Value
            }).ToList(),
            Pictures = media.Pictures.Select(p => new PeerPictureDto
            {
                Id = p.Id,
                Type = p.Type
            }).ToList(),
            Trailers = media.Trailers.Select(t => new PeerTrailerDto
            {
                Key = t.Key,
                Name = t.Name,
                Site = t.Site,
                Type = t.Type,
                Language = t.Language
            }).ToList(),
            Ratings = media.Ratings.OfType<Domain.Entities.Ratings.MetadataProviderRating>().Select(r => new PeerRatingDto
            {
                Provider = r.MetadataProvider.ToString(),
                Value = r.Value,
                MinimumValue = r.MinimumValue,
                MaximumValue = r.MaximumValue,
                RatingCount = r.RatingCount
            }).ToList(),
            PersonRoles = media.PersonRoles.Select(MapPersonRole).ToList(),
        };

        return media switch
        {
            Movie movie => dto with
            {
                Overview = movie.Overview,
                Tagline = movie.Tagline,
                OriginalLanguage = movie.OriginalLanguage,
                ContentRating = GetTagDisplayName(movie, MetadataTagKind.ContentRating),
                Budget = movie.Budget,
                Revenue = movie.Revenue,
                Studios = GetTagDisplayNames(movie, MetadataTagKind.Studio),
            },
            Serie serie => dto with
            {
                Overview = serie.Overview,
                OriginalLanguage = serie.OriginalLanguage,
                ContentRating = GetTagDisplayName(serie, MetadataTagKind.ContentRating),
                Status = serie.Status,
                Network = GetTagDisplayName(serie, MetadataTagKind.Network),
                Studios = GetTagDisplayNames(serie, MetadataTagKind.Studio),
                TotalSeasons = serie.Seasons.Count,
                Seasons = serie.Seasons.Select(s => new PeerSeasonDto
                {
                    SeasonNumber = s.SeasonNumber,
                    Title = s.Title,
                    SortTitle = s.SortTitle,
                    Overview = s.Overview,
                    AirDate = s.ReleaseDate,
                    EpisodeCount = s.Episodes.Count,
                    ExternalIds = s.ExternalIds.Select(e => new PeerExternalIdDto
                    {
                        Provider = e.ProviderName,
                        Value = e.Value
                    }).ToList(),
                    Pictures = s.Pictures.Select(p => new PeerPictureDto
                    {
                        Id = p.Id,
                        Type = p.Type
                    }).ToList(),
                    Episodes = s.Episodes.Select(e => new PeerEpisodeDto
                    {
                        Id = e.Id,
                        EpisodeNumber = e.EpisodeNumber,
                        SeasonNumber = s.SeasonNumber,
                        Title = e.Title,
                        SortTitle = e.SortTitle,
                        Overview = e.Overview,
                        AirDate = e.AirDate,
                        Runtime = e.Runtime,
                        StillPictureId = e.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Still)?.Id,
                        ExternalIds = e.ExternalIds.Select(ex => new PeerExternalIdDto
                        {
                            Provider = ex.ProviderName,
                            Value = ex.Value
                        }).ToList(),
                        PersonRoles = e.PersonRoles.Select(MapPersonRole).ToList()
                    }).ToList()
                }).ToList(),
            },
            MusicAlbum album => dto with
            {
                Overview = album.Overview,
                Tracks = album.Tracks.Select(t => new PeerMusicTrackDto
                {
                    Id = t.Id,
                    Title = t.Title ?? string.Empty,
                    SortTitle = t.SortTitle,
                    TrackNumber = t.TrackNumber,
                    DiscNumber = t.DiscNumber,
                    Duration = t.IndexedFiles.FirstOrDefault()?.FileMetadata is AudioFileMetadata af ? af.Duration : null,
                    MusicBrainzRecordingId = t.ExternalIds.FirstOrDefault(e => e.ProviderName == "musicbrainz")?.Value,
                    Isrc = t.ExternalIds.FirstOrDefault(e => e.ProviderName == "isrc")?.Value,
                    Lyrics = t.Lyrics,
                    LyricsLrc = t.LyricsLrc,
                    ArtistCredits = t.ArtistCredits.Select(c => new PeerMusicTrackArtistCreditDto
                    {
                        Name = c.MusicArtist.Title ?? string.Empty,
                        MusicBrainzArtistId = c.MusicArtist.ExternalIds
                            .FirstOrDefault(e => e.ProviderName == "musicbrainz")?.Value ?? string.Empty,
                        IsGuest = c.IsGuest
                    }).ToList()
                }).ToList(),
                Artists = album.Artist is not null
                    ? [new PeerMusicArtistDto
                    {
                        Name = album.Artist.Title ?? string.Empty,
                        SortName = album.Artist.SortTitle,
                        MusicBrainzArtistId = album.Artist.ExternalIds
                            .FirstOrDefault(e => e.ProviderName == "musicbrainz")?.Value ?? string.Empty
                    }]
                    : []
            },
            _ => dto
        };
    }

    private static string? GetTagDisplayName(BaseMedia media, MetadataTagKind kind) =>
        media.MetadataTags
            .FirstOrDefault(mt => mt.MetadataTag.Kind == kind)
            ?.MetadataTag.DisplayName;

    private static List<string> GetTagDisplayNames(BaseMedia media, MetadataTagKind kind) =>
        media.MetadataTags
            .Where(mt => mt.MetadataTag.Kind == kind)
            .Select(mt => mt.MetadataTag.DisplayName)
            .ToList();

    private static PeerPersonRoleDto MapPersonRole(BasePersonRole role)
    {
        var dto = new PeerPersonRoleDto
        {
            RoleType = role.Type.ToString(),
            PersonName = role.Person.Name,
            Order = role.Order,
            Birthday = role.Person.Birthday,
            Deathday = role.Person.Deathday,
            BirthPlace = role.Person.BirthPlace,
            Biography = role.Person.Biography,
            Gender = role.Person.Gender,
            PortraitPictureId = role.PortraitPicture?.Id ?? role.Person.PortraitPicture?.Id,
            PersonExternalIds = role.Person.ExternalIds.Select(e => new PeerExternalIdDto
            {
                Provider = e.ProviderName,
                Value = e.Value
            }).ToList(),
            RoleExternalIds = role.ExternalIds.Select(e => new PeerExternalIdDto
            {
                Provider = e.ProviderName,
                Value = e.Value
            }).ToList(),
        };

        if (role is Actor actor)
        {
            return dto with { CharacterName = actor.CharacterName };
        }

        if (role is CrewMember crew)
        {
            return dto with { Department = crew.Department, Job = crew.Job };
        }

        return dto;
    }
}
