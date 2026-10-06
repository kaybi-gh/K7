using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Application.UnitTests.Services;

public class LastVideoTrackSelectionMatcherTests
{
    private static AudioFileTrackDto Audio(int index, string? language) => new()
    {
        Index = index,
        Language = language,
        Codec = "aac",
        Channels = 2,
        ChannelLayout = "stereo"
    };

    private static SubtitleFileTrackDto Subtitle(
        int index,
        string? language,
        bool isForced = false,
        bool isHearingImpaired = false) => new()
    {
        Index = index,
        Language = language,
        Codec = "subrip",
        IsTextBased = true,
        IsForced = isForced,
        IsHearingImpaired = isHearingImpaired
    };

    [Test]
    public void TryMatch_ShouldSelectAudioAndForcedSubtitle()
    {
        var selection = new LastVideoTrackSelectionDto
        {
            AudioLanguage = "ja",
            SubtitleLanguage = "fr",
            IsForced = true
        };
        var audio = new List<AudioFileTrackDto> { Audio(0, "en"), Audio(1, "ja") };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "fr"),
            Subtitle(3, "fr", isForced: true)
        };

        var result = LastVideoTrackSelectionMatcher.TryMatch(selection, audio, subs);

        result.Should().NotBeNull();
        result!.AudioTrackIndex.Should().Be(1);
        result.SubtitleTrackIndex.Should().Be(3);
    }

    [Test]
    public void TryMatch_ShouldSelectHearingImpairedSubtitle()
    {
        var selection = new LastVideoTrackSelectionDto
        {
            AudioLanguage = "en",
            SubtitleLanguage = "fr",
            IsHearingImpaired = true
        };
        var audio = new List<AudioFileTrackDto> { Audio(0, "en") };
        var subs = new List<SubtitleFileTrackDto>
        {
            Subtitle(2, "fr"),
            Subtitle(3, "fr", isHearingImpaired: true)
        };

        var result = LastVideoTrackSelectionMatcher.TryMatch(selection, audio, subs);

        result.Should().NotBeNull();
        result!.SubtitleTrackIndex.Should().Be(3);
    }

    [Test]
    public void TryMatch_ShouldHonorSubtitlesOff()
    {
        var selection = new LastVideoTrackSelectionDto
        {
            AudioLanguage = "en",
            SubtitlesOff = true,
            SubtitleLanguage = "fr"
        };
        var audio = new List<AudioFileTrackDto> { Audio(0, "en") };
        var subs = new List<SubtitleFileTrackDto> { Subtitle(2, "fr") };

        var result = LastVideoTrackSelectionMatcher.TryMatch(selection, audio, subs);

        result.Should().NotBeNull();
        result!.AudioTrackIndex.Should().Be(0);
        result.SubtitleTrackIndex.Should().BeNull();
    }

    [Test]
    public void TryMatch_ShouldReturnNull_WhenAudioLanguageMissing()
    {
        var selection = new LastVideoTrackSelectionDto
        {
            AudioLanguage = "ja",
            SubtitleLanguage = "fr"
        };
        var audio = new List<AudioFileTrackDto> { Audio(0, "en") };
        var subs = new List<SubtitleFileTrackDto> { Subtitle(2, "fr") };

        LastVideoTrackSelectionMatcher.TryMatch(selection, audio, subs).Should().BeNull();
    }

    [Test]
    public void TryMatch_ShouldFallBackToLanguageOnlySubtitle_WhenFlagsMissing()
    {
        var selection = new LastVideoTrackSelectionDto
        {
            AudioLanguage = "en",
            SubtitleLanguage = "fr",
            IsForced = true
        };
        var audio = new List<AudioFileTrackDto> { Audio(0, "en") };
        var subs = new List<SubtitleFileTrackDto> { Subtitle(2, "fr") };

        var result = LastVideoTrackSelectionMatcher.TryMatch(selection, audio, subs);

        result.Should().NotBeNull();
        result!.SubtitleTrackIndex.Should().Be(2);
    }
}
