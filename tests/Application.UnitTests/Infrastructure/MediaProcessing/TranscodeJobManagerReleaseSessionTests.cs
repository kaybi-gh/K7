using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.MediaProcessing;
using K7.Shared.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class TranscodeJobManagerReleaseSessionTests
{
    private string _tempDirectory = null!;
    private TranscodeJobManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "k7-transcode-release-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        var transcoder = Substitute.For<IMediaTranscoder>();
        transcoder.StartVideoStreamingTranscodeAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<List<HlsSegment>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int?>())
            .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()));

        var settingsProvider = Substitute.For<ITranscodeSettingsProvider>();
        settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new TranscodeSettingsDto { TranscodeTempQuotaMb = 0, EncoderThrottleBufferSegments = 10 });

        _manager = new TranscodeJobManager(
            Substitute.For<ILogger<TranscodeJobManager>>(),
            transcoder,
            Options.Create(new PathsConfiguration { Transcoding = _tempDirectory }),
            settingsProvider,
            Substitute.For<IServiceScopeFactory>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _manager.CleanupStaleJobsAsync(TimeSpan.Zero);
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Test]
    public async Task ReleaseSessionAsync_ShouldStopFfmpeg_OnlyWhenLastSessionLeaves()
    {
        // SyncPlay / co-watching: every viewer has its own stream session attached to the
        // same shared job. One viewer closing must not stop the others' ffmpeg.
        var indexedFileId = Guid.NewGuid();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var segments = CreateSegments(200);

        var job = await _manager.GetOrStartJobAsync(
            indexedFileId, "input.mkv", "original", videoCodec: "copy", audioCodec: null,
            audioTrackIndex: 0, isAudioOnly: false, sessionA);
        await _manager.GetOrStartJobAsync(
            indexedFileId, "input.mkv", "original", videoCodec: "copy", audioCodec: null,
            audioTrackIndex: 0, isAudioOnly: false, sessionB);
        await _manager.EnsureSegmentWillBeGeneratedAsync(job.JobId, 50, segments);
        job.IsFfmpegRunning.Should().BeTrue();

        await _manager.ReleaseSessionAsync(sessionA);
        job.IsFfmpegRunning.Should().BeTrue();
        job.AttachedStreamSessions.Keys.Should().BeEquivalentTo([sessionB]);

        await _manager.ReleaseSessionAsync(sessionB);
        job.IsFfmpegRunning.Should().BeFalse();
        job.AttachedStreamSessions.Should().BeEmpty();
        // The job and its cache stay for the next launch.
        Directory.Exists(job.OutputDirectory).Should().BeTrue();
    }

    [Test]
    public async Task ReleaseSessionAsync_ShouldIgnoreUnknownSession()
    {
        var indexedFileId = Guid.NewGuid();
        var sessionA = Guid.NewGuid();
        var segments = CreateSegments(200);
        var job = await _manager.GetOrStartJobAsync(
            indexedFileId, "input.mkv", "original", videoCodec: "copy", audioCodec: null,
            audioTrackIndex: 0, isAudioOnly: false, sessionA);
        await _manager.EnsureSegmentWillBeGeneratedAsync(job.JobId, 0, segments);

        await _manager.ReleaseSessionAsync(Guid.NewGuid());

        job.IsFfmpegRunning.Should().BeTrue();
    }

    private static List<HlsSegment> CreateSegments(int count)
    {
        var segments = new List<HlsSegment>(count);
        for (var i = 0; i < count; i++)
        {
            segments.Add(new HlsSegment { Number = i, StartTimestamp = i * 6000, Duration = 6000 });
        }

        return segments;
    }
}
