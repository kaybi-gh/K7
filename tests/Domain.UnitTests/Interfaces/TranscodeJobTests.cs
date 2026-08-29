using K7.Server.Domain.Interfaces;

namespace K7.Server.Domain.UnitTests.Interfaces;

[TestFixture]
public class TranscodeJobTests
{
    [Test]
    public void IsCopyRemux_ShouldBeTrue_WhenVideoCodecIsCopy()
    {
        CreateJob(videoCodec: "copy", audioCodec: null, isAudioOnly: false)
            .IsCopyRemux.Should().BeTrue();
    }

    [Test]
    public void IsCopyRemux_ShouldBeTrue_WhenVideoCodecIsEmpty()
    {
        CreateJob(videoCodec: null, audioCodec: null, isAudioOnly: false)
            .IsCopyRemux.Should().BeTrue();
    }

    [Test]
    public void IsCopyRemux_ShouldBeFalse_WhenVideoIsEncoded()
    {
        CreateJob(videoCodec: "h264", audioCodec: null, isAudioOnly: false)
            .IsCopyRemux.Should().BeFalse();
    }

    [Test]
    public void IsCopyRemux_ShouldBeTrue_WhenAudioOnlyCopy()
    {
        CreateJob(videoCodec: null, audioCodec: "copy", isAudioOnly: true)
            .IsCopyRemux.Should().BeTrue();
    }

    [Test]
    public void IsCopyRemux_ShouldBeFalse_WhenAudioOnlyEncoded()
    {
        CreateJob(videoCodec: null, audioCodec: "aac", isAudioOnly: true)
            .IsCopyRemux.Should().BeFalse();
    }

    private static TranscodeJob CreateJob(string? videoCodec, string? audioCodec, bool isAudioOnly) => new()
    {
        JobId = Guid.NewGuid(),
        IndexedFileId = Guid.NewGuid(),
        Quality = "original",
        VideoCodec = videoCodec,
        AudioCodec = audioCodec,
        AudioTrackIndex = 0,
        IsAudioOnly = isAudioOnly,
        OutputDirectory = "/tmp",
        InputFilePath = "/media/file.mkv"
    };
}
