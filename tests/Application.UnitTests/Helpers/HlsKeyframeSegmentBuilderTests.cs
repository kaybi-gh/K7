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
        // 100ms bursts should merge until MinKeyframeSegmentDurationMs.
        long[] keyframes = [0, 100, 200, 300, 2000, 2200, 4000];

        var segments = HlsKeyframeSegmentBuilder.BuildFromTimestamps(
            keyframes,
            totalVideoDurationMs: 5000,
            Guid.NewGuid(),
            Guid.NewGuid(),
            minSegmentDurationMs: Hls.MinKeyframeSegmentDurationMs);

        segments.Select(s => s.StartTimestamp).Should().Equal(0L, 2000L, 4000L);
        segments.Select(s => s.Duration).Should().Equal(2000L, 2000L, 1000L);
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
    public void BuildFromTimestamps_ShouldReturnEmpty_WhenNoKeyframes()
    {
        HlsKeyframeSegmentBuilder.BuildFromTimestamps(
                [],
                10_000,
                Guid.NewGuid(),
                Guid.NewGuid())
            .Should().BeEmpty();
    }
}
