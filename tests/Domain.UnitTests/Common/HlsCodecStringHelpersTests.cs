using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.UnitTests.Common;

[TestFixture]
public class HlsCodecStringHelpersTests
{
    [Test]
    public void GetAACString_ShouldReturnHeProfile_WhenRequested()
    {
        HlsCodecStringHelpers.GetAACString("he").Should().Be("mp4a.40.5");
        HlsCodecStringHelpers.GetAACString("lc").Should().Be("mp4a.40.2");
    }

    [Test]
    public void GetH264String_ShouldEncodeProfileAndLevel()
    {
        HlsCodecStringHelpers.GetH264String("high", 40).Should().Be("avc1.640028");
        HlsCodecStringHelpers.GetH264String("main", 31).Should().Be("avc1.4D401F");
        HlsCodecStringHelpers.GetH264String(null, 30).Should().Be("avc1.42401E");
    }

    [Test]
    public void GetH265String_ShouldEncodeMain10Profile()
    {
        HlsCodecStringHelpers.GetH265String("main10", 120).Should().Be("hvc1.2.4.L120.B0");
        HlsCodecStringHelpers.GetH265String("Main 10", 120).Should().Be("hvc1.2.4.L120.B0");
        HlsCodecStringHelpers.GetH265String(null, 90).Should().Be("hvc1.1.4.L90.B0");
    }

    [Test]
    public void GetH265String_ShouldUseLevelIdc_WhenFfprobeStoresMajorLevel()
    {
        HlsCodecStringHelpers.GetH265String(null, 4).Should().Be("hvc1.1.4.L120.B0");
        HlsCodecStringHelpers.GetH265String(null, 0).Should().Be("hvc1.1.4.L120.B0");
    }

    [Test]
    public void GetHlsCodecs_ShouldCombineVideoAndAudioCodecNames()
    {
        HlsCodecStringHelpers.GetHlsCodecs("h264", "aac").Should().Be("avc1.424028,mp4a.40.2");
        HlsCodecStringHelpers.GetHlsCodecs("hevc", null).Should().Be("hvc1.1.4.L120.B0");
        HlsCodecStringHelpers.GetHlsCodecs(null, "mp3").Should().Be(HlsCodecStringHelpers.MP3);
        HlsCodecStringHelpers.GetHlsCodecs(null, null).Should().BeEmpty();
    }

    [Test]
    public void GetHlsVideoCodecString_ShouldUseTrackLevelIdc()
    {
        var track = new VideoFileTrack
        {
            Width = 1920,
            Height = 1080,
            Codec = "hevc",
            Profile = "Main",
            Level = 120
        };

        HlsCodecStringHelpers.GetHlsVideoCodecString(track).Should().Be("hvc1.1.4.L120.B0");
    }
}

[TestFixture]
public class BaseMediaFieldLockTests
{
    [Test]
    public void LockField_ShouldBeIdempotent()
    {
        var media = new Movie { Title = "Film" };

        media.LockField("Title");
        media.LockField("Title");

        media.IsFieldLocked("Title").Should().BeTrue();
        media.LockedFields.Should().ContainSingle().Which.Should().Be("Title");
    }

    [Test]
    public void UnlockField_ShouldRemoveLock()
    {
        var media = new Movie { Title = "Film" };
        media.LockField("Title");

        media.UnlockField("Title");

        media.IsFieldLocked("Title").Should().BeFalse();
    }
}
