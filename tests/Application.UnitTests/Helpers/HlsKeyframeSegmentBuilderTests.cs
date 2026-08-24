using K7.Server.Domain.Constants;
using K7.Server.Domain.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class HlsKeyframeSegmentBuilderTests
{
    [Test]
    public void BuildFromTimestamps_ShouldEmitOneSegmentPerKeyframe()
    {
        var fileMetadataId = Guid.NewGuid();
        var indexedFileId = Guid.NewGuid();
        long[] keyframes = [0, 2000, 4000, 6000];

        var segments = HlsKeyframeSegmentBuilder.BuildFromTimestamps(
            keyframes,
            totalVideoDurationMs: 8000,
            fileMetadataId,
            indexedFileId);

        segments.Should().HaveCount(4);
        segments.Select(s => s.StartTimestamp).Should().Equal(0L, 2000L, 4000L, 6000L);
        segments.Select(s => s.Duration).Should().Equal(2000L, 2000L, 2000L, 2000L);
        segments.Select(s => s.Number).Should().Equal(0, 1, 2, 3);
        segments.Should().OnlyContain(s => s.FileMetadataId == fileMetadataId && s.IndexedFileId == indexedFileId);
    }

    [Test]
    public void BuildFromTimestamps_ShouldCollapseMicroGops()
    {
        // Gaps >= RemuxSeekClearanceMs and < MinKeyframeSegmentDurationMs collapse.
        long[] keyframes = [0, 100, 200, 300, 2000, 2200, 4000];

        var segments = HlsKeyframeSegmentBuilder.BuildFromTimestamps(
            keyframes,
            totalVideoDurationMs: 5000,
            Guid.NewGuid(),
            Guid.NewGuid(),
            minSegmentDurationMs: Hls.MinKeyframeSegmentDurationMs);

        // 100/200/300 stay (inside clearance of prior start). From 2000, 2200 is also
        // inside clearance so it stays. Only gaps in [250ms, 1000ms) collapse.
        segments.Select(s => s.StartTimestamp).Should().Equal(0L, 100L, 200L, 300L, 2000L, 2200L, 4000L);
        segments.Select(s => s.Duration).Should().Equal(100L, 100L, 100L, 1700L, 200L, 1800L, 1000L);
    }

    [Test]
    public void BuildFromTimestamps_ShouldCollapseGopsShorterThanOneSecond()
    {
        long[] keyframes = [0, 600, 2000, 4000];

        var segments = HlsKeyframeSegmentBuilder.BuildFromTimestamps(
            keyframes,
            totalVideoDurationMs: 5000,
            Guid.NewGuid(),
            Guid.NewGuid());

        // 600ms >= clearance and < 1s: collapsed into the first playlist GOP.
        segments.Select(s => s.StartTimestamp).Should().Equal(0L, 2000L, 4000L);
        segments.Select(s => s.Duration).Should().Equal(2000L, 2000L, 1000L);
    }

    [Test]
    public void BuildFromTimestamps_ShouldKeepEarlyKeyframesInsideRemuxSeekClearance()
    {
        long[] keyframes = [0, 200, 2000, 4000];

        var segments = HlsKeyframeSegmentBuilder.BuildFromTimestamps(
            keyframes,
            totalVideoDurationMs: 5000,
            Guid.NewGuid(),
            Guid.NewGuid());

        segments.Select(s => s.StartTimestamp).Should().Equal(0L, 200L, 2000L, 4000L);
        segments.Select(s => s.Duration).Should().Equal(200L, 1800L, 2000L, 1000L);
    }

    [Test]
    public void BuildFromTimestamps_ShouldKeepOneSecondGops()
    {
        long[] keyframes = [0, 1000, 3000];

        var segments = HlsKeyframeSegmentBuilder.BuildFromTimestamps(
            keyframes,
            totalVideoDurationMs: 4000,
            Guid.NewGuid(),
            Guid.NewGuid());

        segments.Select(s => s.StartTimestamp).Should().Equal(0L, 1000L, 3000L);
        segments.Select(s => s.Duration).Should().Equal(1000L, 2000L, 1000L);
    }

    [Test]
    public void BuildFromTimestamps_ShouldMergeShortTrailingStub()
    {
        long[] keyframes = [0, 2000, 4000];

        var segments = HlsKeyframeSegmentBuilder.BuildFromTimestamps(
            keyframes,
            totalVideoDurationMs: 4200,
            Guid.NewGuid(),
            Guid.NewGuid(),
            minSegmentDurationMs: 500);

        segments.Should().HaveCount(2);
        segments[^1].StartTimestamp.Should().Be(2000);
        segments[^1].Duration.Should().Be(2200);
    }

    [Test]
    public void BuildFromTimestamps_ShouldEmitSingleFullDurationSegment_WhenNoKeyframesReported()
    {
        var fileMetadataId = Guid.NewGuid();
        var indexedFileId = Guid.NewGuid();

        var segments = HlsKeyframeSegmentBuilder.BuildFromTimestamps(
            [],
            10_000,
            fileMetadataId,
            indexedFileId);

        segments.Should().ContainSingle();
        segments[0].StartTimestamp.Should().Be(0);
        segments[0].Duration.Should().Be(10_000);
        segments[0].FileMetadataId.Should().Be(fileMetadataId);
        segments[0].IndexedFileId.Should().Be(indexedFileId);
    }

    [Test]
    public void BuildFromTimestamps_ShouldReturnEmpty_WhenDurationIsZero()
    {
        HlsKeyframeSegmentBuilder.BuildFromTimestamps(
                [0],
                0,
                Guid.NewGuid(),
                Guid.NewGuid())
            .Should().BeEmpty();
    }
}
