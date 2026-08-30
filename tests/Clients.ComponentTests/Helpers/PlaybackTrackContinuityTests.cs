using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class PlaybackTrackContinuityTests
{
    [Test]
    public void MatchAudioIndex_ShouldPreferLanguage_WhenPresentOnNextFile()
    {
        var current = new AudioFileTrackDto
        {
            Index = 0,
            Language = "fra",
            Codec = "aac",
            Channels = 2,
            ChannelLayout = "stereo"
        };
        var next = new[]
        {
            new AudioFileTrackDto { Index = 0, Language = "eng", Codec = "aac", Channels = 2, ChannelLayout = "stereo" },
            new AudioFileTrackDto { Index = 2, Language = "fra", Codec = "aac", Channels = 2, ChannelLayout = "stereo" }
        };

        PlaybackTrackContinuity.MatchAudioIndex(next, current).Should().Be(2);
    }

    [Test]
    public void MatchAudioIndex_ShouldFallBackToIndex_WhenLanguageMissing()
    {
        var current = new AudioFileTrackDto
        {
            Index = 1,
            Language = null,
            Codec = "aac",
            Channels = 2,
            ChannelLayout = "stereo"
        };
        var next = new[]
        {
            new AudioFileTrackDto { Index = 1, Language = "eng", Codec = "aac", Channels = 2, ChannelLayout = "stereo" }
        };

        PlaybackTrackContinuity.MatchAudioIndex(next, current).Should().Be(1);
    }

    [Test]
    public void MatchSubtitleIndex_ShouldPreferLanguageAndFlags()
    {
        var current = new SubtitleFileTrackDto
        {
            Index = 3,
            Language = "fra",
            IsForced = true,
            IsHearingImpaired = false
        };
        var next = new[]
        {
            new SubtitleFileTrackDto { Index = 1, Language = "fra", IsForced = false },
            new SubtitleFileTrackDto { Index = 4, Language = "fra", IsForced = true }
        };

        PlaybackTrackContinuity.MatchSubtitleIndex(next, current).Should().Be(4);
    }
}
