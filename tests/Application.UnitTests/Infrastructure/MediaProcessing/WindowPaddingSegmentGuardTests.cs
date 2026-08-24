using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class WindowPaddingSegmentGuardTests
{
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "k7-pad-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Test]
    public void RestoreOrDiscard_ShouldRestoreReadyBeforePad_AfterOverwrite()
    {
        var original = Enumerable.Repeat((byte)0xAA, 64).ToArray();
        File.WriteAllBytes(Path.Combine(_tempDirectory, "4.m4s"), original);

        var guard = WindowPaddingSegmentGuard.Capture(
            _tempDirectory,
            ffmpegStartIndex: 4,
            deliverStartIndex: 5,
            deliverEndIndexExclusive: 10,
            ffmpegEndIndexExclusive: 11);

        File.WriteAllBytes(Path.Combine(_tempDirectory, "4.m4s"), Enumerable.Repeat((byte)0xBB, 64).ToArray());
        File.WriteAllBytes(Path.Combine(_tempDirectory, "10.m4s"), Enumerable.Repeat((byte)0xCC, 64).ToArray());

        guard.RestoreOrDiscard();

        File.ReadAllBytes(Path.Combine(_tempDirectory, "4.m4s")).Should().Equal(original);
        File.Exists(Path.Combine(_tempDirectory, "10.m4s")).Should().BeTrue();
    }

    [Test]
    public void RestoreOrDiscard_ShouldKeepNewAfterPad_AndDeleteNewBeforePad()
    {
        var guard = WindowPaddingSegmentGuard.Capture(
            _tempDirectory,
            ffmpegStartIndex: 4,
            deliverStartIndex: 5,
            deliverEndIndexExclusive: 10,
            ffmpegEndIndexExclusive: 11);

        File.WriteAllBytes(Path.Combine(_tempDirectory, "4.m4s"), Enumerable.Repeat((byte)0x11, 64).ToArray());
        File.WriteAllBytes(Path.Combine(_tempDirectory, "10.m4s"), Enumerable.Repeat((byte)0x22, 64).ToArray());

        guard.RestoreOrDiscard();

        File.Exists(Path.Combine(_tempDirectory, "4.m4s")).Should().BeFalse();
        File.ReadAllBytes(Path.Combine(_tempDirectory, "10.m4s")).Should().Equal(
            Enumerable.Repeat((byte)0x22, 64).ToArray());
    }

    [Test]
    public void RestoreOrDiscard_ShouldKeepReadyAfterPad()
    {
        var original = Enumerable.Repeat((byte)0xDD, 64).ToArray();
        File.WriteAllBytes(Path.Combine(_tempDirectory, "10.m4s"), original);

        var guard = WindowPaddingSegmentGuard.Capture(
            _tempDirectory,
            ffmpegStartIndex: 4,
            deliverStartIndex: 5,
            deliverEndIndexExclusive: 10,
            ffmpegEndIndexExclusive: 11);

        File.WriteAllBytes(Path.Combine(_tempDirectory, "10.m4s"), Enumerable.Repeat((byte)0xEE, 80).ToArray());

        guard.RestoreOrDiscard();

        File.ReadAllBytes(Path.Combine(_tempDirectory, "10.m4s")).Should().Equal(original);
    }

    [Test]
    public void RestoreOrDiscard_ShouldDeleteCloserSegment()
    {
        var guard = WindowPaddingSegmentGuard.Capture(
            _tempDirectory,
            ffmpegStartIndex: 4,
            deliverStartIndex: 5,
            deliverEndIndexExclusive: 10,
            ffmpegEndIndexExclusive: 11,
            segmentCount: 20);

        File.WriteAllBytes(Path.Combine(_tempDirectory, "10.m4s"), Enumerable.Repeat((byte)0x22, 64).ToArray());
        File.WriteAllBytes(Path.Combine(_tempDirectory, "11.m4s"), Enumerable.Repeat((byte)0x33, 64).ToArray());

        guard.RestoreOrDiscard();

        File.Exists(Path.Combine(_tempDirectory, "10.m4s")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDirectory, "11.m4s")).Should().BeFalse();
    }

    [Test]
    public void DeleteCloserSegment_ShouldRemoveExclusiveEndFile()
    {
        File.WriteAllBytes(Path.Combine(_tempDirectory, "8.m4s"), Enumerable.Repeat((byte)0x44, 64).ToArray());

        WindowPaddingSegmentGuard.DeleteCloserSegment(_tempDirectory, ffmpegEndIndexExclusive: 8, segmentCount: 20);

        File.Exists(Path.Combine(_tempDirectory, "8.m4s")).Should().BeFalse();
    }
}
