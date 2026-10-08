using K7.Server.Application.Services;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class FederatedMediaPieceMapTests
{
    [Test]
    public void MissingRanges_ShouldReturnFullWindow_WhenEmpty()
    {
        var map = new FederatedMediaPieceMap(10L * FederatedMediaPieceMap.PieceSizeBytes);
        var missing = map.MissingRanges(0, FederatedMediaPieceMap.PieceSizeBytes * 2);

        missing.Should().HaveCount(1);
        missing[0].Offset.Should().Be(0);
        missing[0].Count.Should().Be(FederatedMediaPieceMap.PieceSizeBytes * 2);
    }

    [Test]
    public void MissingRanges_ShouldSkipPresentPieces_AndMergeGaps()
    {
        var map = new FederatedMediaPieceMap(8L * FederatedMediaPieceMap.PieceSizeBytes);
        map.MarkPresent(1);
        map.MarkPresent(2);

        var missing = map.MissingRanges(0, 5L * FederatedMediaPieceMap.PieceSizeBytes);

        missing.Should().HaveCount(2);
        missing[0].Should().Be((0, FederatedMediaPieceMap.PieceSizeBytes));
        missing[1].Offset.Should().Be(3L * FederatedMediaPieceMap.PieceSizeBytes);
        missing[1].Count.Should().Be(2L * FederatedMediaPieceMap.PieceSizeBytes);
    }

    [Test]
    public void MarkRangePresent_ShouldCoverPartialOverlap()
    {
        var map = new FederatedMediaPieceMap(3L * FederatedMediaPieceMap.PieceSizeBytes);
        map.MarkRangePresent(FederatedMediaPieceMap.PieceSizeBytes / 2, FederatedMediaPieceMap.PieceSizeBytes);

        map.IsPiecePresent(0).Should().BeTrue();
        map.IsPiecePresent(1).Should().BeTrue();
        map.IsPiecePresent(2).Should().BeFalse();
        map.MissingRanges(0, map.ContentLength).Should().HaveCount(1);
    }

    [Test]
    public void HeaderAndCuesRange_ShouldCoverWholeFile_WhenSmall()
    {
        var length = FederatedMediaPieceMap.HeaderBytes;
        var (offset, count) = FederatedMediaPieceMap.HeaderAndCuesRange(length);

        offset.Should().Be(0);
        count.Should().Be(length);
        FederatedMediaPieceMap.TailRange(length).Should().BeNull();
    }

    [Test]
    public void HeaderAndCuesRange_ShouldSplitHeadAndTail_WhenLarge()
    {
        var length = FederatedMediaPieceMap.HeaderBytes + FederatedMediaPieceMap.TailBytes + 1;
        var (offset, count) = FederatedMediaPieceMap.HeaderAndCuesRange(length);
        var tail = FederatedMediaPieceMap.TailRange(length);

        offset.Should().Be(0);
        count.Should().Be(FederatedMediaPieceMap.HeaderBytes);
        tail.Should().NotBeNull();
        tail!.Value.Offset.Should().Be(length - FederatedMediaPieceMap.TailBytes);
        tail.Value.Count.Should().Be(FederatedMediaPieceMap.TailBytes);
    }

    [Test]
    public void ApproximateTimeWindow_ShouldMapProportionallyWithMargin()
    {
        var length = 1000L * 1024 * 1024;
        var (offset, count) = FederatedMediaPieceMap.ApproximateTimeWindow(
            length,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(31),
            TimeSpan.FromHours(2));

        var expectedCenter = length / 4;
        offset.Should().Be(expectedCenter - FederatedMediaPieceMap.TimeWindowMarginBytes);
        // Blocking runway is capped (and floored) to EnsureRunwayBytes, not the whole title.
        count.Should().Be(FederatedMediaPieceMap.EnsureRunwayBytes);
        (offset + count).Should().BeLessThanOrEqualTo(length);
    }

    [Test]
    public void ApproximateTimeWindow_ShouldCapBlockingRunway_WhenRemuxWindowSpansWholeTitle()
    {
        var length = 8L * 1024 * 1024 * 1024;
        var (offset, count) = FederatedMediaPieceMap.ApproximateTimeWindow(
            length,
            TimeSpan.Zero,
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(2));

        offset.Should().Be(0);
        count.Should().Be(FederatedMediaPieceMap.EnsureRunwayBytes);
    }

    [Test]
    public void ApproximateTimeWindow_ShouldJumpNearSeekPosition_NotFromStart()
    {
        var length = 8L * 1024 * 1024 * 1024;
        var (offset, count) = FederatedMediaPieceMap.ApproximateTimeWindow(
            length,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)),
            TimeSpan.FromHours(2));

        offset.Should().BeGreaterThan(length / 4);
        count.Should().Be(FederatedMediaPieceMap.EnsureRunwayBytes);
        (offset + count).Should().BeLessThan(length);
    }

    [Test]
    public void ApproximateTimeWindow_ShouldUseLargerRunway_WhenEncodeRequested()
    {
        var length = 8L * 1024 * 1024 * 1024;
        var (offset, count) = FederatedMediaPieceMap.ApproximateTimeWindow(
            length,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)),
            TimeSpan.FromHours(2),
            FederatedMediaPieceMap.EncodeEnsureRunwayBytes);

        offset.Should().BeGreaterThan(length / 4);
        count.Should().Be(FederatedMediaPieceMap.EncodeEnsureRunwayBytes);
        (offset + count).Should().BeLessThan(length);
    }

    [Test]
    public void ChunkRange_ShouldSplitIntoPieceSizedFetches()
    {
        var chunks = FederatedMediaPieceMap.ChunkRange(
                0,
                FederatedMediaPieceMap.PieceSizeBytes * 3,
                FederatedMediaPieceMap.PieceSizeBytes * 10)
            .ToList();

        chunks.Should().HaveCount(3);
        chunks.Should().OnlyContain(c => c.Count == FederatedMediaPieceMap.PieceSizeBytes);
    }
}
