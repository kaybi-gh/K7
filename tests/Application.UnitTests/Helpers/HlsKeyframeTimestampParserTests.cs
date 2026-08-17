using K7.Server.Domain.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class HlsKeyframeTimestampParserTests
{
    [Test]
    public void TryParsePacketLine_ShouldReadPts_WhenLegacyPtsAndFlags()
    {
        HlsKeyframeTimestampParser.TryParsePacketLine("1.500000,K_", out var timestampMs)
            .Should().BeTrue();
        timestampMs.Should().Be(1500);
    }

    [Test]
    public void TryParsePacketLine_ShouldReadDts_WhenPtsIsNotAvailable()
    {
        HlsKeyframeTimestampParser.TryParsePacketLine("N/A,2.041000,K_", out var timestampMs)
            .Should().BeTrue();
        timestampMs.Should().Be(2041);
    }

    [Test]
    public void TryParsePacketLine_ShouldRejectNonKeyframe()
    {
        HlsKeyframeTimestampParser.TryParsePacketLine("1.500000,_", out _)
            .Should().BeFalse();
        HlsKeyframeTimestampParser.TryParsePacketLine("N/A,1.500000,_", out _)
            .Should().BeFalse();
    }

    [Test]
    public void TryParsePacketLine_ShouldRejectKeyframeWithoutTimestamp()
    {
        HlsKeyframeTimestampParser.TryParsePacketLine("N/A,N/A,K_", out _)
            .Should().BeFalse();
    }

    [Test]
    public void TryParseKeyframeFrameLine_ShouldReadFirstParseableTimestamp()
    {
        HlsKeyframeTimestampParser.TryParseKeyframeFrameLine("N/A,3.000000", out var timestampMs)
            .Should().BeTrue();
        timestampMs.Should().Be(3000);
    }
}
