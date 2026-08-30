using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class PlaybackSessionTrackSelectionTests
{
    [Test]
    public void Apply_ShouldUseSessionSettings_WhenCallerDidNotRequestTracks()
    {
        var audio = new List<AudioFileTrackDto>
        {
            new() { Index = 0, Language = "eng", IsDefault = true, Codec = "aac", Channels = 2, ChannelLayout = "stereo" },
            new() { Index = 1, Language = "fra", Codec = "aac", Channels = 2, ChannelLayout = "stereo" }
        };
        var subs = new List<SubtitleFileTrackDto>
        {
            new() { Index = 2, Language = "fra" }
        };

        PlaybackSessionTrackSelection.Apply(
            audio,
            subs,
            new PlaybackSettingsDto { AudioTrackIndex = 1, SubtitleTrackIndex = 2 },
            requestedAudioTrackIndex: null,
            requestedSubtitleTrackIndex: null,
            out var selectedAudio,
            out var selectedSubtitle);

        selectedAudio!.Index.Should().Be(1);
        selectedSubtitle!.Index.Should().Be(2);
    }

    [Test]
    public void Apply_ShouldHonorCaller_WhenDialogRequestedTracks()
    {
        var audio = new List<AudioFileTrackDto>
        {
            new() { Index = 0, Language = "eng", IsDefault = true, Codec = "aac", Channels = 2, ChannelLayout = "stereo" },
            new() { Index = 1, Language = "fra", Codec = "aac", Channels = 2, ChannelLayout = "stereo" }
        };
        var subs = new List<SubtitleFileTrackDto>
        {
            new() { Index = 2, Language = "fra" }
        };

        PlaybackSessionTrackSelection.Apply(
            audio,
            subs,
            new PlaybackSettingsDto { AudioTrackIndex = 0, SubtitleTrackIndex = 2 },
            requestedAudioTrackIndex: 1,
            requestedSubtitleTrackIndex: null,
            out var selectedAudio,
            out var selectedSubtitle);

        selectedAudio!.Index.Should().Be(1);
        selectedSubtitle.Should().BeNull();
    }
}
