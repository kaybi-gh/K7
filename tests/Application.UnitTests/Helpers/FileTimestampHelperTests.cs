using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

public class FileTimestampHelperTests
{
    [Test]
    public void HasSameContent_ShouldReturnTrue_WhenWithinTolerance()
    {
        var stored = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var disk = stored.AddMilliseconds(500);

        FileTimestampHelper.HasSameContent(stored, 1000, disk, 1000).Should().BeTrue();
    }

    [Test]
    public void HasSameContent_ShouldReturnFalse_WhenSizeDiffers()
    {
        var stored = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

        FileTimestampHelper.HasSameContent(stored, 1000, stored, 2000).Should().BeFalse();
    }

    [Test]
    public void HasSameContent_ShouldBackfill_WhenStoredTimestampMissing()
    {
        var disk = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

        FileTimestampHelper.HasSameContent(default, 1000, disk, 1000).Should().BeTrue();
        FileTimestampHelper.NeedsLastWriteTimeBackfill(default).Should().BeTrue();
    }
}
