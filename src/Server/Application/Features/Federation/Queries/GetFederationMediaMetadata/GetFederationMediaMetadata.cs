using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationMediaMetadata;

public record GetFederationMediaMetadataQuery(string? ClientId, Guid MediaId) : IRequest<PeerFullMediaMetadataDto>;

public class GetFederationMediaMetadataQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context)
    : IRequestHandler<GetFederationMediaMetadataQuery, PeerFullMediaMetadataDto>
{
    public async Task<PeerFullMediaMetadataDto> Handle(
        GetFederationMediaMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(request.ClientId, cancellationToken);

        if (!await peerAuthorization.IsMediaAccessibleToPeerAsync(peer.Id, request.MediaId, cancellationToken))
            throw new ForbiddenAccessException();

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
            .FirstOrDefaultAsync(m => m.Id == request.MediaId && m.PeerServerId == null, cancellationToken);

        if (media is null)
            throw new NotFoundException(request.MediaId.ToString(), nameof(BaseMedia));

        return FederationPeerMediaMetadataMapper.BuildDto(media);
    }
}

internal static class FederationPeerMediaMetadataMapper
{
    internal static PeerFullMediaMetadataDto BuildDto(BaseMedia media)
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
            return dto with { CharacterName = actor.CharacterName };

        if (role is CrewMember crew)
            return dto with { Department = crew.Department, Job = crew.Job };

        return dto;
    }
}
