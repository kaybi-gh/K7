using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Shared.Dtos;

public sealed record LastVideoTrackSelectionDto
{
    public string? AudioLanguage { get; set; }
    public string? SubtitleLanguage { get; set; }
    public bool SubtitlesOff { get; set; }
    public bool IsForced { get; set; }
    public bool IsHearingImpaired { get; set; }

    public static LastVideoTrackSelectionDto FromTracks(
        AudioFileTrackDto? audio,
        SubtitleFileTrackDto? subtitle) => new()
    {
        AudioLanguage = audio?.Language,
        SubtitleLanguage = subtitle?.Language,
        SubtitlesOff = subtitle is null,
        IsForced = subtitle?.IsForced ?? false,
        IsHearingImpaired = subtitle?.IsHearingImpaired ?? false
    };
}
