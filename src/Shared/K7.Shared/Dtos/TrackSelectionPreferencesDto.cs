using K7.Shared.Enums;

namespace K7.Shared.Dtos;

public sealed record TrackSelectionPreferencesDto
{
    public string PreferredAudioLanguage { get; set; } = "fr";
    public string? FallbackAudioLanguage { get; set; }
    public SubtitlePreference SubtitleWhenPreferredAudio { get; set; } = SubtitlePreference.ForcedOnly;
    public string SubtitleLanguageWhenPreferredAudio { get; set; } = "fr";
    public SubtitlePreference SubtitleWhenOtherAudio { get; set; } = SubtitlePreference.Full;
    public string SubtitleLanguageWhenOtherAudio { get; set; } = "fr";
}
