using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities.Medias;

public sealed record LiteMusicArtistDto : LiteMediaDto
{
    public MusicArtistType ArtistType { get; init; }
    public string? Country { get; init; }
    public IReadOnlyList<LiteMusicAlbumDto>? Albums { get; init; }
    public IReadOnlyList<LiteMusicAlbumDto>? GuestAppearanceAlbums { get; init; }

    /// <summary>Optional AudioMuse divergence/similarity when returned from MI endpoints.</summary>
    public double? IntelligenceScore { get; init; }

    /// <summary>AudioMuse metric for <see cref="IntelligenceScore"/> (e.g. divergence).</summary>
    public string? IntelligenceScoreMetric { get; init; }
}
