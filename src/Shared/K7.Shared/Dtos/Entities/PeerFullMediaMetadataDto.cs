using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record PeerFullMediaMetadataDto
{
    public required Guid Id { get; init; }
    public required MediaType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? OriginalTitle { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string? Overview { get; init; }
    public string? Tagline { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? ContentRating { get; init; }
    public long? Budget { get; init; }
    public long? Revenue { get; init; }
    public string? Status { get; init; }
    public string? Network { get; init; }
    public int? TotalSeasons { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];
    public IReadOnlyList<string> Studios { get; init; } = [];
    public IReadOnlyList<PeerExternalIdDto> ExternalIds { get; init; } = [];
    public IReadOnlyList<PeerTrailerDto> Trailers { get; init; } = [];
    public IReadOnlyList<PeerPersonRoleDto> PersonRoles { get; init; } = [];
    public IReadOnlyList<PeerPictureDto> Pictures { get; init; } = [];
    public IReadOnlyList<PeerRatingDto> Ratings { get; init; } = [];
    public IReadOnlyList<PeerSeasonDto> Seasons { get; init; } = [];
    public IReadOnlyList<PeerMusicTrackDto> Tracks { get; init; } = [];
    public IReadOnlyList<PeerMusicArtistDto> Artists { get; init; } = [];
}

public sealed record PeerTrailerDto
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Site { get; init; }
    public required string Type { get; init; }
    public string? Language { get; init; }
}

public sealed record PeerPictureDto
{
    public required Guid Id { get; init; }
    public required MetadataPictureType Type { get; init; }
}

public sealed record PeerRatingDto
{
    public required string Provider { get; init; }
    public required double Value { get; init; }
    public required double MinimumValue { get; init; }
    public required double MaximumValue { get; init; }
    public int? RatingCount { get; init; }
}

public sealed record PeerPersonRoleDto
{
    public required string RoleType { get; init; }
    public required string PersonName { get; init; }
    public string? CharacterName { get; init; }
    public string? Department { get; init; }
    public string? Job { get; init; }
    public int? Order { get; init; }
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }
    public string? Biography { get; init; }
    public PersonGender Gender { get; init; }
    public Guid? PortraitPictureId { get; init; }
    public IReadOnlyList<PeerExternalIdDto> PersonExternalIds { get; init; } = [];
    public IReadOnlyList<PeerExternalIdDto> RoleExternalIds { get; init; } = [];
}

public sealed record PeerSeasonDto
{
    public int SeasonNumber { get; init; }
    public string? Title { get; init; }
    public string? Overview { get; init; }
    public DateOnly? AirDate { get; init; }
    public int? EpisodeCount { get; init; }
    public IReadOnlyList<PeerExternalIdDto> ExternalIds { get; init; } = [];
    public IReadOnlyList<PeerPictureDto> Pictures { get; init; } = [];
    public IReadOnlyList<PeerEpisodeDto> Episodes { get; init; } = [];
}

public sealed record PeerEpisodeDto
{
    public int EpisodeNumber { get; init; }
    public int SeasonNumber { get; init; }
    public string? Title { get; init; }
    public string? Overview { get; init; }
    public DateOnly? AirDate { get; init; }
    public int? Runtime { get; init; }
    public Guid? StillPictureId { get; init; }
    public IReadOnlyList<PeerExternalIdDto> ExternalIds { get; init; } = [];
}

public sealed record PeerMusicTrackDto
{
    public Guid? Id { get; init; }
    public required string Title { get; init; }
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? MusicBrainzRecordingId { get; init; }
    public string? Isrc { get; init; }
    public IReadOnlyList<PeerMusicTrackArtistCreditDto> ArtistCredits { get; init; } = [];
}

public sealed record PeerMusicTrackArtistCreditDto
{
    public required string Name { get; init; }
    public required string MusicBrainzArtistId { get; init; }
    public bool IsGuest { get; init; }
}

public sealed record PeerMusicArtistDto
{
    public required string Name { get; init; }
    public required string MusicBrainzArtistId { get; init; }
}
