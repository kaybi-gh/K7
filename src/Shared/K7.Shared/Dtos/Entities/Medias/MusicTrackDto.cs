namespace K7.Shared.Dtos.Entities.Medias;

public sealed record MusicTrackDto : MediaDto
{
    public Guid AlbumId { get; init; }
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public string? Lyrics { get; init; }
    public string? LyricsLrc { get; init; }
    public double? Bpm { get; init; }
    public string? MusicalKey { get; init; }
    public double? LoudnessLufs { get; init; }
}
