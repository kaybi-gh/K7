using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record PeerMediaDto
{
    public required Guid Id { get; init; }
    public required MediaType Type { get; init; }
    public string? Title { get; init; }
    public string? SortTitle { get; init; }
    public string? OriginalTitle { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string? Overview { get; init; }
    public string? Tagline { get; init; }
    public string? OriginalLanguage { get; init; }
    public Guid? PosterPictureId { get; init; }
    public Guid? BackdropPictureId { get; init; }
    public Guid? LogoPictureId { get; init; }
    public IReadOnlyList<PeerExternalIdDto> ExternalIds { get; init; } = [];
    public IReadOnlyList<PeerFileDto> Files { get; init; } = [];
    public IReadOnlyList<string> Genres { get; init; } = [];
}

public sealed record PeerExternalIdDto
{
    public required string Provider { get; init; }
    public required string Value { get; init; }
}

public sealed record PeerFileDto
{
    public required Guid Id { get; init; }
    public Guid? MediaId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required long Size { get; init; }
    public string? Container { get; init; }
    public TimeSpan? Duration { get; init; }
    public long? VideoBitrate { get; init; }
    public VideoResolutionIdentifier? VideoResolution { get; init; }
    public IReadOnlyList<PeerAudioTrackDto> AudioTracks { get; init; } = [];
    public IReadOnlyList<PeerVideoTrackDto> VideoTracks { get; init; } = [];
    public IReadOnlyList<PeerSubtitleTrackDto> SubtitleTracks { get; init; } = [];
}

public sealed record PeerAudioTrackDto
{
    public int Index { get; init; }
    public bool IsDefault { get; init; }
    public string? Name { get; init; }
    public string? Language { get; init; }
    public required string Codec { get; init; }
    public required int Channels { get; init; }
    public string? ChannelLayout { get; init; }
    public int? SampleRateHz { get; init; }
    public string? Profile { get; init; }
}

public sealed record PeerVideoTrackDto
{
    public int Index { get; init; }
    public bool IsDefault { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string Codec { get; init; }
    public required string Profile { get; init; }
    public required int Level { get; init; }
    public string? PixelFormat { get; init; }
    public int? BitDepth { get; init; }
}

public sealed record PeerSubtitleTrackDto
{
    public int Index { get; init; }
    public bool IsDefault { get; init; }
    public string? Name { get; init; }
    public string? Language { get; init; }
    public required string Codec { get; init; }
    public bool IsTextBased { get; init; }
    public bool IsForced { get; init; }
    public bool IsHearingImpaired { get; init; }
}
