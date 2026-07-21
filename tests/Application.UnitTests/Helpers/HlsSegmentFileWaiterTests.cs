using K7.Server.Application.Helpers;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class HlsSegmentFileWaiterTests
{
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "k7-hls-waiter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Test]
    public void HasNonEmptyContent_ShouldReturnFalse_WhenFileMissing()
    {
        var path = Path.Combine(_tempDirectory, "init.m4s");

        HlsSegmentFileWaiter.HasNonEmptyContent(path).Should().BeFalse();
    }

    [Test]
    public void HasNonEmptyContent_ShouldReturnFalse_WhenFileIsEmpty()
    {
        var path = Path.Combine(_tempDirectory, "init.m4s");
        File.WriteAllBytes(path, []);

        HlsSegmentFileWaiter.HasNonEmptyContent(path).Should().BeFalse();
    }

    [Test]
    public void HasNonEmptyContent_ShouldReturnTrue_WhenFileHasBytes()
    {
        var path = Path.Combine(_tempDirectory, "init.m4s");
        File.WriteAllBytes(path, [0x00, 0x00, 0x00, 0x18]);

        HlsSegmentFileWaiter.HasNonEmptyContent(path).Should().BeTrue();
    }

    [Test]
    public async Task WaitUntilAvailableAsync_ShouldWaitUntilInitHasContent()
    {
        var initPath = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);
        var job = CreateJob();
        var generationStarted = 0;

        var waitTask = HlsSegmentFileWaiter.WaitUntilAvailableAsync(
            initPath,
            job,
            _ =>
            {
                generationStarted++;
                return Task.CompletedTask;
            },
            CancellationToken.None,
            maxTotalSeconds: 5,
            pollingIntervalMs: 50);

        await Task.Delay(150);
        File.WriteAllBytes(initPath, [0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70]);

        var result = await waitTask;

        result.Should().BeNull();
        generationStarted.Should().BeGreaterThanOrEqualTo(1);
        HlsSegmentFileWaiter.HasNonEmptyContent(initPath).Should().BeTrue();
    }

    [Test]
    public async Task WaitUntilAvailableAsync_ShouldTimeout_WhenInitStaysEmpty()
    {
        var initPath = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);
        File.WriteAllBytes(initPath, []);
        var job = CreateJob();

        var result = await HlsSegmentFileWaiter.WaitUntilAvailableAsync(
            initPath,
            job,
            _ => Task.CompletedTask,
            CancellationToken.None,
            maxTotalSeconds: 1,
            pollingIntervalMs: 50);

        result.Should().BeOfType<TimeoutException>();
    }

    [Test]
    public void GetInitSegmentPath_ShouldCombineOutputDirectoryAndInitFileName()
    {
        var path = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);

        path.Should().Be(Path.Combine(_tempDirectory, HlsSegmentFileWaiter.InitSegmentFileName));
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnFalse_WhenSegmentMissing()
    {
        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 0).Should().BeFalse();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnFalse_WhenSegment0ExistsButInitIsEmpty()
    {
        File.WriteAllBytes(Path.Combine(_tempDirectory, "0.m4s"), [0x01]);
        File.WriteAllBytes(HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory), []);

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 0).Should().BeFalse();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnFalse_WhenSegment0ExistsButInitMissing()
    {
        File.WriteAllBytes(Path.Combine(_tempDirectory, "0.m4s"), [0x01]);

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 0).Should().BeFalse();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnTrue_WhenSegment0AndInitHaveContent()
    {
        File.WriteAllBytes(Path.Combine(_tempDirectory, "0.m4s"), [0x01, 0x02]);
        File.WriteAllBytes(HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory), [0x00, 0x00]);

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 0).Should().BeTrue();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnTrue_WhenLaterSegmentHasContentWithoutInit()
    {
        File.WriteAllBytes(Path.Combine(_tempDirectory, "3.m4s"), [0x01]);

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 3).Should().BeTrue();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnFalse_WhenLaterSegmentIsEmpty()
    {
        File.WriteAllBytes(Path.Combine(_tempDirectory, "2.m4s"), []);

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 2).Should().BeFalse();
    }

    private TranscodeJob CreateJob() =>
        new()
        {
            JobId = Guid.NewGuid(),
            IndexedFileId = Guid.NewGuid(),
            Quality = "original",
            VideoCodec = null,
            AudioCodec = "aac",
            AudioTrackIndex = 1,
            IsAudioOnly = true,
            OutputDirectory = _tempDirectory,
            InputFilePath = "input.mkv",
        };
}
