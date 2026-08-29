using K7.Server.Application.Common;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Common;

[TestFixture]
public class StreamDecisionTrackSelectionTests
{
    [Test]
    public void Apply_ShouldReplaceAudioAndClearSubtitle_WhenSubtitleSpecifiedOff()
    {
        var current = new StreamDecisionDto
        {
            Mode = PlaybackMode.Direct,
            SourceAudioCodec = "aac",
            StreamAudioCodec = "aac",
            SelectedAudioTrackIndex = 1,
            AudioTrackLanguage = "eng",
            SelectedSubtitleTrackIndex = 3,
            SubtitleTrackLanguage = "fra"
        };

        var next = StreamDecisionTrackSelection.Apply(
            current,
            new AudioFileTrack { Index = 2, Codec = "eac3", Channels = 6, Language = "jpn", Name = "Japanese", ChannelLayout = "5.1" },
            subtitle: null,
            subtitleSpecified: true);

        next.SelectedAudioTrackIndex.Should().Be(2);
        next.SourceAudioCodec.Should().Be("eac3");
        next.AudioTrackLanguage.Should().Be("jpn");
        next.SelectedSubtitleTrackIndex.Should().BeNull();
        next.SubtitleTrackLanguage.Should().BeNull();
    }

    [Test]
    public void Apply_ShouldKeepSubtitle_WhenSubtitleNotSpecified()
    {
        var current = new StreamDecisionDto
        {
            Mode = PlaybackMode.Direct,
            SelectedAudioTrackIndex = 1,
            SelectedSubtitleTrackIndex = 3,
            SubtitleTrackLanguage = "fra"
        };

        var next = StreamDecisionTrackSelection.Apply(
            current,
            new AudioFileTrack { Index = 4, Codec = "aac", Channels = 2, Language = "eng", Name = "English", ChannelLayout = "stereo" },
            subtitle: null,
            subtitleSpecified: false);

        next.SelectedAudioTrackIndex.Should().Be(4);
        next.SelectedSubtitleTrackIndex.Should().Be(3);
        next.SubtitleTrackLanguage.Should().Be("fra");
    }
}
