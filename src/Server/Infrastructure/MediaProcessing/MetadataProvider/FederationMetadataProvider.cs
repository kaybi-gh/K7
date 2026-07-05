using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using K7.Shared.Dtos.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

public class FederationMetadataProvider(
    IPeerClient peerClient,
    IApplicationDbContext context,
    ILogger<FederationMetadataProvider> logger)
    : IMetadataProvider<ExternalMovieMetadata>,
      ISerieMetadataProvider,
      IMetadataProvider<ExternalMusicAlbumMetadata>,
      IMetadataProviderInfo
{
    public string ProviderName => "federation";
    public IReadOnlyList<LibraryMediaType> SupportedMediaTypes { get; } = [LibraryMediaType.Movie, LibraryMediaType.Serie, LibraryMediaType.Music];

    public Task<string?> SearchAsync(MediaIdentification movieIdentification, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> SearchSerieAsync(MediaIdentification identification, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    async Task<ExternalMovieMetadata> IMetadataProvider<ExternalMovieMetadata>.FetchMetadata(string providerId, string language, CancellationToken cancellationToken)
    {
        var dto = await FetchRemoteMetadataAsync(providerId, cancellationToken);
        if (dto is null)
            return new ExternalMovieMetadata { Title = string.Empty };

        var (_, baseUrl) = await ResolveBaseUrlFromProviderId(providerId, cancellationToken);

        return new ExternalMovieMetadata
        {
            Title = dto.Title,
            SortTitle = dto.SortTitle,
            OriginalTitle = dto.OriginalTitle,
            ReleaseDate = dto.ReleaseDate,
            Overview = dto.Overview,
            Tagline = dto.Tagline,
            OriginalLanguage = dto.OriginalLanguage,
            ContentRating = dto.ContentRating,
            Budget = dto.Budget,
            Revenue = dto.Revenue,
            Genres = dto.Genres.ToList(),
            Studios = dto.Studios.ToList(),
            Trailers = dto.Trailers.Select(t => new TrailerInfo
            {
                Key = t.Key,
                Name = t.Name,
                Site = t.Site,
                Type = t.Type,
                Language = t.Language
            }).ToList(),
            ExternalIds = dto.ExternalIds.Select(e => new ExternalId
            {
                ProviderName = e.Provider,
                Value = e.Value
            }).ToList(),
            Pictures = BuildPictures(dto.Pictures, baseUrl),
            PersonRoles = BuildPersonRoles(dto.PersonRoles, baseUrl),
        };
    }

    async Task<ExternalMusicAlbumMetadata> IMetadataProvider<ExternalMusicAlbumMetadata>.FetchMetadata(string providerId, string language, CancellationToken cancellationToken)
    {
        var dto = await FetchRemoteMetadataAsync(providerId, cancellationToken);
        if (dto is null)
            return new ExternalMusicAlbumMetadata();

        var (_, baseUrl) = await ResolveBaseUrlFromProviderId(providerId, cancellationToken);

        return new ExternalMusicAlbumMetadata
        {
            Title = dto.Title,
            SortTitle = dto.SortTitle,
            ReleaseDate = dto.ReleaseDate,
            Overview = dto.Overview,
            Genres = dto.Genres.ToList(),
            ExternalIds = dto.ExternalIds.Select(e => new ExternalId
            {
                ProviderName = e.Provider,
                Value = e.Value
            }).ToList(),
            Pictures = BuildPictures(dto.Pictures, baseUrl),
            Tracks = dto.Tracks.Select(t => new ExternalMusicTrackMetadata
            {
                RemoteId = t.Id,
                Title = t.Title,
                SortTitle = t.SortTitle,
                TrackNumber = t.TrackNumber,
                DiscNumber = t.DiscNumber,
                Duration = t.Duration,
                MusicBrainzRecordingId = t.MusicBrainzRecordingId,
                Isrc = t.Isrc,
                Lyrics = t.Lyrics,
                LyricsLrc = t.LyricsLrc,
                ArtistCredits = t.ArtistCredits.Select(a => new ExternalMusicTrackArtistCredit
                {
                    Name = a.Name,
                    MusicBrainzArtistId = a.MusicBrainzArtistId,
                    IsGuest = a.IsGuest
                }).ToList()
            }).ToList(),
            Artists = dto.Artists.Select(a => new ExternalMusicArtistMetadata
            {
                Name = a.Name,
                SortName = a.SortName,
                MusicBrainzArtistId = a.MusicBrainzArtistId
            }).ToList(),
        };
    }

    public async Task<ExternalSerieMetadata> FetchSerieMetadataAsync(string providerId, string language, CancellationToken cancellationToken = default, string? fallbackLanguage = null)
    {
        var dto = await FetchRemoteMetadataAsync(providerId, cancellationToken);
        if (dto is null)
            return new ExternalSerieMetadata { Title = string.Empty };

        var (_, baseUrl) = await ResolveBaseUrlFromProviderId(providerId, cancellationToken);

        return new ExternalSerieMetadata
        {
            Title = dto.Title,
            SortTitle = dto.SortTitle,
            OriginalTitle = dto.OriginalTitle,
            ReleaseDate = dto.ReleaseDate,
            Overview = dto.Overview,
            OriginalLanguage = dto.OriginalLanguage,
            ContentRating = dto.ContentRating,
            Status = dto.Status,
            Network = dto.Network,
            TotalSeasons = dto.TotalSeasons,
            Genres = dto.Genres.ToList(),
            Studios = dto.Studios.ToList(),
            Trailers = dto.Trailers.Select(t => new TrailerInfo
            {
                Key = t.Key,
                Name = t.Name,
                Site = t.Site,
                Type = t.Type,
                Language = t.Language
            }).ToList(),
            ExternalIds = dto.ExternalIds.Select(e => new ExternalId
            {
                ProviderName = e.Provider,
                Value = e.Value
            }).ToList(),
            Pictures = BuildPictures(dto.Pictures, baseUrl),
            PersonRoles = BuildPersonRoles(dto.PersonRoles, baseUrl),
        };
    }

    public async Task<ExternalSeasonMetadata> FetchSeasonMetadataAsync(string providerId, int seasonNumber, string language, CancellationToken cancellationToken = default, string? fallbackLanguage = null)
    {
        var dto = await FetchRemoteMetadataAsync(providerId, cancellationToken);
        if (dto is null)
            return new ExternalSeasonMetadata { SeasonNumber = seasonNumber };

        var (_, baseUrl) = await ResolveBaseUrlFromProviderId(providerId, cancellationToken);

        var season = dto.Seasons.FirstOrDefault(s => s.SeasonNumber == seasonNumber);
        if (season is null)
            return new ExternalSeasonMetadata { SeasonNumber = seasonNumber };

        return new ExternalSeasonMetadata
        {
            SeasonNumber = season.SeasonNumber,
            Title = season.Title,
            SortTitle = season.SortTitle,
            Overview = season.Overview,
            AirDate = season.AirDate,
            EpisodeCount = season.EpisodeCount,
            ExternalIds = season.ExternalIds.Select(e => new ExternalId
            {
                ProviderName = e.Provider,
                Value = e.Value
            }).ToList(),
            Pictures = BuildPictures(season.Pictures, baseUrl),
        };
    }

    public async Task<ExternalEpisodeMetadata> FetchEpisodeMetadataAsync(string providerId, int seasonNumber, int episodeNumber, string language, CancellationToken cancellationToken = default, string? fallbackLanguage = null)
    {
        var dto = await FetchRemoteMetadataAsync(providerId, cancellationToken);
        if (dto is null)
            return new ExternalEpisodeMetadata { SeasonNumber = seasonNumber, EpisodeNumber = episodeNumber };

        var (_, baseUrl) = await ResolveBaseUrlFromProviderId(providerId, cancellationToken);

        var season = dto.Seasons.FirstOrDefault(s => s.SeasonNumber == seasonNumber);
        var episode = season?.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
        if (episode is null)
            return new ExternalEpisodeMetadata { SeasonNumber = seasonNumber, EpisodeNumber = episodeNumber };

        return new ExternalEpisodeMetadata
        {
            RemoteId = episode.Id,
            EpisodeNumber = episode.EpisodeNumber,
            SeasonNumber = episode.SeasonNumber,
            Title = episode.Title,
            SortTitle = episode.SortTitle,
            Overview = episode.Overview,
            AirDate = episode.AirDate,
            Runtime = episode.Runtime,
            StillImageUrl = episode.StillPictureId is not null
                ? $"{baseUrl.TrimEnd('/')}/api/metadata-pictures/{episode.StillPictureId}"
                : null,
            ExternalIds = episode.ExternalIds.Select(e => new ExternalId
            {
                ProviderName = e.Provider,
                Value = e.Value
            }).ToList(),
            PersonRoles = BuildPersonRoles(episode.PersonRoles, baseUrl),
        };
    }

    public Task<(int Season, int Episode)?> ResolveAbsoluteEpisodeAsync(string providerId, int absoluteNumber, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<(int Season, int Episode)?>(null);
    }

    private async Task<PeerFullMediaMetadataDto?> FetchRemoteMetadataAsync(string compositeProviderId, CancellationToken cancellationToken)
    {
        var (peer, baseUrl) = await ResolvePeerFromProviderId(compositeProviderId, cancellationToken);
        if (peer is null)
            return null;

        if (!Guid.TryParse(compositeProviderId[(compositeProviderId.IndexOf(':') + 1)..], out var remoteMediaId))
            return null;

        var token = await peerClient.GetAccessTokenAsync(baseUrl, peer.OutboundClientId!, peer.OutboundClientSecret!, cancellationToken);
        if (token is null)
        {
            logger.LogWarning("Failed to authenticate with peer {PeerName} for metadata fetch", peer.Name);
            return null;
        }

        return await peerClient.GetRemoteMediaMetadataAsync(baseUrl, token, remoteMediaId, cancellationToken);
    }

    private async Task<(Domain.Entities.Federation.PeerServer? Peer, string BaseUrl)> ResolvePeerFromProviderId(string compositeProviderId, CancellationToken cancellationToken)
    {
        var separatorIndex = compositeProviderId.IndexOf(':');
        if (separatorIndex < 0)
            return (null, string.Empty);

        if (!Guid.TryParse(compositeProviderId[..separatorIndex], out var peerId))
            return (null, string.Empty);

        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Id == peerId && p.Status == PeerStatus.Active, cancellationToken);

        if (peer is null || peer.OutboundClientId is null || peer.OutboundClientSecret is null)
            return (null, string.Empty);

        return (peer, peer.BaseUrl);
    }

    private async Task<(Guid PeerId, string BaseUrl)> ResolveBaseUrlFromProviderId(string compositeProviderId, CancellationToken cancellationToken)
    {
        var (peer, baseUrl) = await ResolvePeerFromProviderId(compositeProviderId, cancellationToken);
        return (peer?.Id ?? Guid.Empty, baseUrl);
    }

    private static List<MetadataPicture> BuildPictures(IReadOnlyList<PeerPictureDto> pictures, string baseUrl)
    {
        var result = new List<MetadataPicture>();
        foreach (var pic in pictures)
        {
            var picture = new MetadataPicture
            {
                OriginalRemoteUri = new Uri($"{baseUrl.TrimEnd('/')}/api/metadata-pictures/{pic.Id}"),
                Type = pic.Type
            };
            picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
            result.Add(picture);
        }
        return result;
    }

    private static List<BasePersonRole> BuildPersonRoles(IReadOnlyList<PeerPersonRoleDto> roles, string baseUrl)
    {
        var result = new List<BasePersonRole>();
        foreach (var role in roles)
        {
            if (!Enum.TryParse<PersonRoleType>(role.RoleType, out var roleType))
                continue;

            var person = new Person
            {
                Name = role.PersonName,
                Gender = role.Gender,
                Birthday = role.Birthday,
                Deathday = role.Deathday,
                BirthPlace = role.BirthPlace,
                Biography = role.Biography,
                ExternalIds = role.PersonExternalIds.Select(e => new ExternalId
                {
                    ProviderName = e.Provider,
                    Value = e.Value
                }).ToList()
            };

            if (role.PortraitPictureId is not null)
            {
                var portrait = new MetadataPicture
                {
                    OriginalRemoteUri = new Uri($"{baseUrl.TrimEnd('/')}/api/metadata-pictures/{role.PortraitPictureId}"),
                    Type = MetadataPictureType.Portrait
                };
                portrait.AddDomainEvent(new MetadataPictureCreatedEvent(portrait));
                person.PortraitPicture = portrait;
            }

            BasePersonRole personRole = roleType switch
            {
                PersonRoleType.Actor => new Actor { CharacterName = role.CharacterName ?? string.Empty, Person = person, Order = role.Order },
                PersonRoleType.VoiceActor => new VoiceActor { CharacterName = role.CharacterName ?? string.Empty, Person = person, Order = role.Order },
                PersonRoleType.CrewMember => new CrewMember { Department = role.Department, Job = role.Job, Person = person, Order = role.Order },
                PersonRoleType.MusicArtist => new MusicArtistMember { Person = person, Order = role.Order },
                _ => new CrewMember { Person = person, Order = role.Order }
            };

            personRole.ExternalIds = role.RoleExternalIds.Select(e => new ExternalId
            {
                ProviderName = e.Provider,
                Value = e.Value
            }).ToList();

            result.Add(personRole);
        }
        return result;
    }
}
