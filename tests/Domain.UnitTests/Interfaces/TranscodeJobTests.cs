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

    [Test]
    public void GetCurrentSegmentIndex_ShouldScanFromLowest_WhenWindowNotAnchored()
    {
        var dir = CreateTempDir();
        WriteSegment(dir, 0);
        WriteSegment(dir, 1);
        WriteSegment(dir, 2);

        var job = CreateJob(videoCodec: "h264", audioCodec: "aac", isAudioOnly: false, outputDirectory: dir);

        job.GetCurrentSegmentIndex().Should().Be(2);
    }

    [Test]
    public void GetCurrentSegmentIndex_ShouldReportContiguousRunFromAnchor_IgnoringFarKeptSegments()
    {
        var dir = CreateTempDir();
        // Kept segments from an earlier no-purge seek window.
        WriteSegment(dir, 1075);
        WriteSegment(dir, 1076);
        // Current window (re-anchored on seek back).
        WriteSegment(dir, 1110);
        WriteSegment(dir, 1111);
        WriteSegment(dir, 1112);

        var job = CreateJob(videoCodec: "h264", audioCodec: "aac", isAudioOnly: false, outputDirectory: dir);
        job.WindowStartIndex = 1110;

        job.GetCurrentSegmentIndex().Should().Be(1112);
    }

    [Test]
    public void GetCurrentSegmentIndex_ShouldStopAtHole_WhenWindowHasUnreadyGap()
    {
        var dir = CreateTempDir();
        WriteSegment(dir, 1110);
        WriteSegment(dir, 1112);

        var job = CreateJob(videoCodec: "h264", audioCodec: "aac", isAudioOnly: false, outputDirectory: dir);
        job.WindowStartIndex = 1110;

        job.GetCurrentSegmentIndex().Should().Be(1110);
    }

    [Test]
    public void GetCurrentSegmentIndex_ShouldReturnAnchorMinusOne_WhenAnchorSegmentNotReady()
    {
        var dir = CreateTempDir();
        WriteSegment(dir, 1075);

        var job = CreateJob(videoCodec: "h264", audioCodec: "aac", isAudioOnly: false, outputDirectory: dir);
        job.WindowStartIndex = 1110;

        job.GetCurrentSegmentIndex().Should().Be(1109);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "k7-transcode-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteSegment(string dir, int index)
        => File.WriteAllBytes(Path.Combine(dir, $"{index}.m4s"), new byte[64]);

    private static TranscodeJob CreateJob(
        string? videoCodec,
        string? audioCodec,
        bool isAudioOnly,
        string outputDirectory = "/tmp") => new()
    {
        JobId = Guid.NewGuid(),
        IndexedFileId = Guid.NewGuid(),
        Quality = "original",
        VideoCodec = videoCodec,
        AudioCodec = audioCodec,
        AudioTrackIndex = 0,
        IsAudioOnly = isAudioOnly,
        OutputDirectory = outputDirectory,
        InputFilePath = "/media/file.mkv"
    };
}
