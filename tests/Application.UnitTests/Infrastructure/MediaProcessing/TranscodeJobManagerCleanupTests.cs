using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.MediaProcessing;
using K7.Shared.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class TranscodeJobManagerCleanupTests
{
    private string _tempDirectory = null!;
    private TranscodeJobManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "k7-transcode-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        var settingsProvider = Substitute.For<ITranscodeSettingsProvider>();
        settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new TranscodeSettingsDto { TranscodeTempQuotaMb = 0, EncoderThrottleBufferSegments = 10 });

        _manager = new TranscodeJobManager(
            Substitute.For<ILogger<TranscodeJobManager>>(),
            Substitute.For<IMediaTranscoder>(),
            Options.Create(new PathsConfiguration { Transcoding = _tempDirectory }),
            settingsProvider,
            Substitute.For<IServiceScopeFactory>());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Test]
    public async Task CleanupStaleJobsAsync_ShouldDeleteSubtitleCache_WhenLastJobForFileIsStale()
    {
        var indexedFileId = Guid.NewGuid();
        var job = await StartVideoJobAsync(indexedFileId);
        var subtitlesDir = CreateSubtitleCache(indexedFileId);
        job.LastPingTime = DateTime.UtcNow - TimeSpan.FromHours(2);

        await _manager.CleanupStaleJobsAsync(TimeSpan.FromHours(1));

        Directory.Exists(subtitlesDir).Should().BeFalse();
        Directory.Exists(job.OutputDirectory).Should().BeFalse();
        Directory.Exists(Path.Combine(_tempDirectory, indexedFileId.ToString("N"))).Should().BeFalse();
    }

    [Test]
    public async Task CleanupStaleJobsAsync_ShouldKeepSubtitleCache_WhenAnotherJobForFileIsActive()
    {
        var indexedFileId = Guid.NewGuid();
        var videoJob = await StartVideoJobAsync(indexedFileId);
        var audioJob = await _manager.GetOrStartJobAsync(
            indexedFileId,
            "input.mkv",
            "original",
            videoCodec: null,
            audioCodec: "aac",
            audioTrackIndex: 1,
            isAudioOnly: true,
            Guid.NewGuid());
        var subtitlesDir = CreateSubtitleCache(indexedFileId);
        videoJob.LastPingTime = DateTime.UtcNow - TimeSpan.FromHours(2);

        await _manager.CleanupStaleJobsAsync(TimeSpan.FromHours(1));

        Directory.Exists(subtitlesDir).Should().BeTrue();
        Directory.Exists(audioJob.OutputDirectory).Should().BeTrue();
        Directory.Exists(videoJob.OutputDirectory).Should().BeFalse();
    }

    [Test]
    public async Task CleanupStaleJobsAsync_ShouldDeleteOrphanedSubtitleCache_WhenNoJobRemains()
    {
        var indexedFileId = Guid.NewGuid();
        var subtitlesDir = CreateSubtitleCache(indexedFileId);

        await _manager.CleanupStaleJobsAsync(TimeSpan.FromHours(1));

        Directory.Exists(subtitlesDir).Should().BeFalse();
        Directory.Exists(Path.Combine(_tempDirectory, indexedFileId.ToString("N"))).Should().BeFalse();
    }

    private async Task<TranscodeJob> StartVideoJobAsync(Guid indexedFileId) =>
        await _manager.GetOrStartJobAsync(
            indexedFileId,
            "input.mkv",
            "original",
            videoCodec: "copy",
            audioCodec: null,
            audioTrackIndex: 0,
            isAudioOnly: false,
            Guid.NewGuid());

    private string CreateSubtitleCache(Guid indexedFileId)
    {
        var subtitlesDir = Path.Combine(
            _tempDirectory,
            indexedFileId.ToString("N"),
            Hls.SubtitlesCacheDirectoryName);
        Directory.CreateDirectory(subtitlesDir);
        File.WriteAllText(Path.Combine(subtitlesDir, "0.vtt"), "WEBVTT\n");
        return subtitlesDir;
    }
}
