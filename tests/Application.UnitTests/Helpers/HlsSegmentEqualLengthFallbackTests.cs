using K7.Server.Application.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class HlsSegmentEqualLengthFallbackTests
{
    [Test]
    public void ComputeEqualLengthHlsSegments_ShouldBuildSixSecondGrid()
    {
        var segments = HlsSegmentHelper.ComputeEqualLengthHlsSegments(15_000);

        segments.Should().HaveCount(3);
        segments[0].Duration.Should().Be(HlsSegmentHelper.TargetSegmentDurationMs);
        segments[1].Duration.Should().Be(HlsSegmentHelper.TargetSegmentDurationMs);
        segments[2].Duration.Should().Be(3_000);
        segments[2].StartTimestamp.Should().Be(12_000);
    }

    [Test]
    public void ResolveVideoStreamingSegments_ShouldUseEqualLength_WhenKeyframeGridEmpty()
    {
        var segments = HlsSegmentHelper.ResolveVideoStreamingSegments([], 12_000);

        segments.Should().HaveCount(2);
        segments.Should().OnlyContain(s => s.Duration == HlsSegmentHelper.TargetSegmentDurationMs);
    }

    [Test]
    public void MissingGrid_ShouldForceH264TranscodeDecision()
    {
        var decision = new StreamDecisionDto
        {
            Mode = PlaybackMode.Transmux,
            StreamVideoCodec = "hevc",
            Reason = TranscodeReason.None
        };

        var forced = decision with
        {
            Mode = PlaybackMode.Transcode,
            StreamVideoCodec = HlsSegmentHelper.FallbackTranscodingVideoCodec,
            Reason = decision.Reason | TranscodeReason.HlsSegmentsUnavailable
        };

        forced.Mode.Should().Be(PlaybackMode.Transcode);
        forced.StreamVideoCodec.Should().Be("h264");
        forced.Reason.Should().HaveFlag(TranscodeReason.HlsSegmentsUnavailable);
    }
}
