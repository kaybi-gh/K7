namespace K7.Shared.Dtos.Entities.Medias;

public sealed record LiteMusicTrackDto : LiteMediaDto
{
    public Guid AlbumId { get; init; }
    public int? TrackNumber { get; init; }
    public Guid? IndexedFileId { get; init; }
    public Guid? RemoteIndexedFileId { get; init; }
    public double? Duration { get; init; }
    public string? AlbumTitle { get; init; }
    public string? ArtistName { get; init; }
    public Guid? ArtistId { get; init; }
    public string? Genre { get; init; }
    public double? LoudnessLufs { get; init; }
    public double? FadeInDuration { get; init; }
    public double? FadeOutDuration { get; init; }
    public double? ReplayGainTrackGain { get; init; }
    public IReadOnlyList<MusicArtistCreditDto>? ArtistCredits { get; init; }
}
