namespace K7.Shared.Dtos.Entities.Medias;

public sealed record MusicTrackDto : MediaDto
{
    public Guid AlbumId { get; init; }
    public string? AlbumTitle { get; init; }
    public Guid? ArtistId { get; init; }
    public string? ArtistName { get; init; }
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public string? Lyrics { get; init; }
    public string? LyricsLrc { get; init; }
    public double? Bpm { get; init; }
    public string? MusicalKey { get; init; }
    public double? LoudnessLufs { get; init; }
    public double? Energy { get; init; }
    public double? Danceability { get; init; }
    public double? Valence { get; init; }
    public float[]? WaveformPeaks { get; init; }
    public double? FadeInDuration { get; init; }
    public double? FadeOutDuration { get; init; }
    public double? ReplayGainTrackGain { get; init; }
}
