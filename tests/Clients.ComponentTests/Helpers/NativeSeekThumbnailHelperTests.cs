using AwesomeAssertions;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class NativeSeekThumbnailHelperTests
{
    [Test]
    public void GetSpriteIndex_ShouldFloorToInterval()
    {
        NativeSeekThumbnailHelper.GetSpriteIndex(0).Should().Be(0);
        NativeSeekThumbnailHelper.GetSpriteIndex(29).Should().Be(0);
        NativeSeekThumbnailHelper.GetSpriteIndex(30).Should().Be(1);
        NativeSeekThumbnailHelper.GetSpriteIndex(-5).Should().Be(0);
    }

    [Test]
    public void GetSpriteCell_ShouldWrapAfterThumbsPerRow()
    {
        var (col, row) = NativeSeekThumbnailHelper.GetSpriteCell(30 * NativeSeekThumbnailHelper.ThumbsPerRow);
        col.Should().Be(0);
        row.Should().Be(1);
    }

    [Test]
    public void GetSpriteLayout_ShouldReturnNegativeOffsetsForCellPosition()
    {
        var (tx, ty, sheetWidth, sheetHeight) = NativeSeekThumbnailHelper.GetSpriteLayout(95, estimatedRows: 20);

        tx.Should().Be(-3 * NativeSeekThumbnailHelper.ThumbWidth);
        ty.Should().Be(0);
        sheetWidth.Should().Be(NativeSeekThumbnailHelper.ThumbsPerRow * NativeSeekThumbnailHelper.ThumbWidth);
        sheetHeight.Should().Be(20 * NativeSeekThumbnailHelper.ThumbHeight);
    }

    [Test]
    public void GetSpriteLayout_ShouldGrowSheetHeight_WhenRowExceedsEstimate()
    {
        // Index 249 -> row 24 (249 / 10), so the sheet must be at least 25 rows tall.
        var farTime = 30 * 249;
        var (_, _, _, sheetHeight) = NativeSeekThumbnailHelper.GetSpriteLayout(farTime, estimatedRows: 5);

        sheetHeight.Should().Be(25 * NativeSeekThumbnailHelper.ThumbHeight);
    }
}
