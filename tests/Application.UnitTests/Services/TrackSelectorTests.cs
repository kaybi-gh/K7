using K7.Server.Application.Common.Services;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Services;

public class TrackSelectorTests
{
    private static AudioFileTrackDto Audio(int index, string? language, bool isDefault = false) => new()
    {
        Index = index,
        IsDefault = isDefault,
        Language = language,
        Codec = "aac",
        Channels = 2,
        ChannelLayout = "stereo"
    };

    private static SubtitleFileTrackDto Subtitle(int index, string? language, bool isForced = false, bool isHearingImpaired = false, bool isDefault = false) => new()
    {
        Index = index,
        IsDefault = isDefault,
        Language = language,
        Codec = "subrip",
        IsTextBased = true,
        IsForced = isForced,
        IsHearingImpaired = isHearingImpaired
    };

    private static TrackSelectionPreferencesDto DefaultPreferences() => new()
    {
        PreferredAudioLanguage = "fr",
        FallbackAudioLanguage = null,
        SubtitleWhenPreferredAudio = SubtitlePreference.ForcedOnly,
        SubtitleLanguageWhenPreferredAudio = "fr",
        SubtitleWhenOtherAudio = SubtitlePreference.Full,
        SubtitleLanguageWhenOtherAudio = "fr"
    };

    [Test]
    public void SelectTracks_PreferredAudio_Available_SelectsIt()
    {
        // Arrange
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "en", true), Audio(1, "fr") };
        var subs = new List<SubtitleFileTrackDto> { Subtitle(2, "fr", isForced: true) };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.AudioTrackIndex.Should().Be(1);
        result.SubtitleTrackIndex.Should().Be(2);
    }

    [Test]
    public void SelectTracks_PreferredAudio_NotAvailable_UsesFallback()
    {
        // Arrange
        var prefs = DefaultPreferences();
        prefs.FallbackAudioLanguage = "en";
        var audio = new List<AudioFileTrackDto> { Audio(0, "ja", true), Audio(1, "en") };
        var subs = new List<SubtitleFileTrackDto> { Subtitle(2, "fr") };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.AudioTrackIndex.Should().Be(1);
        result.SubtitleTrackIndex.Should().Be(2); // Full subtitle since audio != preferred
    }

    [Test]
    public void SelectTracks_NoPreferredOrFallback_UsesDefault()
    {
        // Arrange
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "ja", true), Audio(1, "de") };
        var subs = new List<SubtitleFileTrackDto> { Subtitle(2, "fr") };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.AudioTrackIndex.Should().Be(0); // IsDefault = true
        result.SubtitleTrackIndex.Should().Be(2); // Full subtitle since audio != preferred
    }

    [Test]
    public void SelectTracks_NoPreferredNoFallbackNoDefault_UsesFirst()
    {
        // Arrange
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "ja"), Audio(1, "de") };
        var subs = new List<SubtitleFileTrackDto> { Subtitle(2, "fr") };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.AudioTrackIndex.Should().Be(0); // First
    }

    [Test]
    public void SelectTracks_PreferredAudio_SubtitleOff_NoSubtitle()
    {
        // Arrange
        var prefs = DefaultPreferences();
        prefs.SubtitleWhenPreferredAudio = SubtitlePreference.Off;
        var audio = new List<AudioFileTrackDto> { Audio(0, "fr") };
        var subs = new List<SubtitleFileTrackDto> { Subtitle(2, "fr", isForced: true) };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.AudioTrackIndex.Should().Be(0);
        result.SubtitleTrackIndex.Should().BeNull();
    }

    [Test]
    public void SelectTracks_PreferredAudio_ForcedOnly_SelectsForcedSubtitle()
    {
        // Arrange
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "fr") };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "fr"), // Full
            Subtitle(3, "fr", isForced: true), // Forced
            Subtitle(4, "fr", isHearingImpaired: true) // HI
        };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.SubtitleTrackIndex.Should().Be(3);
    }

    [Test]
    public void SelectTracks_OtherAudio_Full_SelectsFullSubtitle()
    {
        // Arrange
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "en") };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "fr", isForced: true), // Forced
            Subtitle(3, "fr"), // Full
            Subtitle(4, "fr", isHearingImpaired: true) // HI
        };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.SubtitleTrackIndex.Should().Be(3);
    }

    [Test]
    public void SelectTracks_HearingImpaired_SelectsHISubtitle()
    {
        // Arrange
        var prefs = DefaultPreferences();
        prefs.SubtitleWhenPreferredAudio = SubtitlePreference.HearingImpaired;
        var audio = new List<AudioFileTrackDto> { Audio(0, "fr") };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "fr"), // Full
            Subtitle(3, "fr", isForced: true), // Forced
            Subtitle(4, "fr", isHearingImpaired: true) // HI
        };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.SubtitleTrackIndex.Should().Be(4);
    }

    [Test]
    public void SelectTracks_NoMatchingSubtitle_ReturnsNull()
    {
        // Arrange
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "fr") };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "en", isForced: true), // Wrong language
            Subtitle(3, "en") // Wrong language
        };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.SubtitleTrackIndex.Should().BeNull();
    }

    [Test]
    public void SelectTracks_EmptySubtitles_ReturnsNull()
    {
        // Arrange
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "fr") };
        var subs = new List<SubtitleFileTrackDto>();

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.SubtitleTrackIndex.Should().BeNull();
    }

    [Test]
    public void SelectTracks_NoAudioTracks_Throws()
    {
        // Arrange
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto>();
        var subs = new List<SubtitleFileTrackDto>();

        // Act & Assert
        var act = () => TrackSelector.SelectTracks(prefs, audio, subs);
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void SelectTracks_LanguageMatchingIsCaseInsensitive()
    {
        // Arrange
        var prefs = DefaultPreferences();
        prefs.PreferredAudioLanguage = "FR";
        var audio = new List<AudioFileTrackDto> { Audio(0, "fr") };
        var subs = new List<SubtitleFileTrackDto> { Subtitle(2, "FR", isForced: true) };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.AudioTrackIndex.Should().Be(0);
        result.SubtitleTrackIndex.Should().Be(2);
    }

    [Test]
    public void SelectTracks_AnimeScenario_JapaneseAudioFrenchFullSubs()
    {
        // Arrange - Typical anime setup: Japanese audio + French full subtitles
        var prefs = new TrackSelectionPreferencesDto
        {
            PreferredAudioLanguage = "ja",
            SubtitleWhenPreferredAudio = SubtitlePreference.Full,
            SubtitleLanguageWhenPreferredAudio = "fr",
            SubtitleWhenOtherAudio = SubtitlePreference.Full,
            SubtitleLanguageWhenOtherAudio = "fr"
        };
        var audio = new List<AudioFileTrackDto> { Audio(0, "ja", true), Audio(1, "fr") };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "fr", isForced: true),
            Subtitle(3, "fr"),
            Subtitle(4, "en")
        };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.AudioTrackIndex.Should().Be(0); // Japanese audio
        result.SubtitleTrackIndex.Should().Be(3); // French full subtitles
    }

    [Test]
    public void SelectTracks_FrenchUser_FrenchAudioAvailable_ForcedSubsOnly()
    {
        // Arrange - French user watching French-dubbed content
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "en", true), Audio(1, "fr") };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "fr", isForced: true),
            Subtitle(3, "fr"),
            Subtitle(4, "en")
        };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.AudioTrackIndex.Should().Be(1); // French audio
        result.SubtitleTrackIndex.Should().Be(2); // French forced only (for foreign dialogue)
    }

    [Test]
    public void SelectTracks_FrenchUser_NoFrenchAudio_FullFrenchSubs()
    {
        // Arrange - French user watching content with no French audio
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "en", true) };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "fr", isForced: true),
            Subtitle(3, "fr"),
            Subtitle(4, "en")
        };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.AudioTrackIndex.Should().Be(0); // Falls back to default
        result.SubtitleTrackIndex.Should().Be(3); // Full French subtitles
    }

    [Test]
    public void SelectTracks_ForcedSubtitleExcludesHearingImpaired()
    {
        // Arrange - A forced+HI subtitle should NOT match ForcedOnly preference
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "fr") };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "fr", isForced: true, isHearingImpaired: true), // Forced but HI
            Subtitle(3, "fr", isForced: true) // Forced, not HI
        };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.SubtitleTrackIndex.Should().Be(3); // Picks the non-HI forced track
    }

    [Test]
    public void SelectTracks_FullSubtitleExcludesHearingImpaired()
    {
        // Arrange
        var prefs = DefaultPreferences();
        var audio = new List<AudioFileTrackDto> { Audio(0, "en") };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "fr", isHearingImpaired: true), // Full but HI
            Subtitle(3, "fr") // Full, not HI
        };

        // Act
        var result = TrackSelector.SelectTracks(prefs, audio, subs);

        // Assert
        result.SubtitleTrackIndex.Should().Be(3);
    }
}
