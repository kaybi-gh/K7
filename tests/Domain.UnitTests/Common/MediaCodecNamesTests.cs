using K7.Server.Domain.Common;

namespace K7.Server.Domain.UnitTests.Common;

[TestFixture]
public class MediaCodecNamesTests
{
    [TestCase("mpeg2video", "mpeg2")]
    [TestCase("pcm_s16le", "pcm")]
    [TestCase("pcm_bluray", "pcm")]
    [TestCase("h265", "hevc")]
    [TestCase("dca", "dts")]
    [TestCase("hevc", "hevc")]
    [TestCase("av01", "av1")]
    [TestCase("av02", "av2")]
    public void Canonical_ShouldMapFfprobeNames(string source, string expected)
    {
        MediaCodecNames.Canonical(source).Should().Be(expected);
    }

    [Test]
    public void EqualsCodec_ShouldMatchPcmVariants()
    {
        MediaCodecNames.EqualsCodec("pcm", "pcm_s24le").Should().BeTrue();
    }
}
