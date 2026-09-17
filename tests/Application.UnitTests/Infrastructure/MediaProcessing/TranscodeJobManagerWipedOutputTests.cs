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
public class TranscodeJobManagerWipedOutputTests
{
    private string _tempDirectory = null!;
    private IMediaTranscoder _transcoder = null!;
    private TranscodeJobManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "k7-transcode-wipe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        _transcoder = Substitute.For<IMediaTranscoder>();
        _transcoder.StartVideoStreamingTranscodeAsync(
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
        _transcoder.StartAudioStreamingTranscodeAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<List<HlsSegment>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<int?>())
            .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()));

        var settingsProvider = Substitute.For<ITranscodeSettingsProvider>();
        settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new TranscodeSettingsDto { TranscodeTempQuotaMb = 0, EncoderThrottleBufferSegments = 10 });

        _manager = new TranscodeJobManager(
            Substitute.For<ILogger<TranscodeJobManager>>(),
            _transcoder,
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
    public async Task EnsureSegmentWillBeGeneratedAsync_ShouldKeepRemuxHead_WhenSharedOutputStillEmpty()
    {
        var indexedFileId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var job = await _manager.GetOrStartJobAsync(
            indexedFileId,
            "input.mkv",
            "original",
            videoCodec: "copy",
            audioCodec: null,
            audioTrackIndex: 0,
            isAudioOnly: false,
            sessionId);
        var segments = CreateSegments(1427);

        await _manager.EnsureSegmentWillBeGeneratedAsync(job.JobId, -1, segments);
        job.IsFfmpegRunning.Should().BeTrue();
        job.TargetSegmentIndex.Should().Be(1426);
        var headId = job.RemuxHeads.Keys.Single();

        await _manager.GetOrStartJobAsync(
            indexedFileId,
            "input.mkv",
            "original",
            videoCodec: "copy",
            audioCodec: null,
            audioTrackIndex: 0,
            isAudioOnly: false,
            sessionId);
        await _manager.EnsureSegmentWillBeGeneratedAsync(job.JobId, 0, segments);

        job.IsFfmpegRunning.Should().BeTrue();
        job.RemuxHeads.ContainsKey(headId).Should().BeTrue();
        job.TargetSegmentIndex.Should().Be(1426);
    }

    [Test]
    public async Task EnsureSegmentWillBeGeneratedAsync_ShouldKeepEncodeWindow_WhenResumeHasNotLandedYet()
    {
        var indexedFileId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var job = await _manager.GetOrStartJobAsync(
            indexedFileId,
            "input.mkv",
            "original",
            videoCodec: null,
            audioCodec: "aac",
            audioTrackIndex: 1,
            isAudioOnly: true,
            sessionId);
        var segments = CreateSegments(1427);

        await _manager.EnsureSegmentWillBeGeneratedAsync(job.JobId, 1055, segments);
        job.IsFfmpegRunning.Should().BeTrue();
        job.LastClientMediaSegmentRequest.Should().Be(1055);
        var target = job.TargetSegmentIndex;
        target.Should().BeGreaterThan(1055);

        await _manager.EnsureSegmentWillBeGeneratedAsync(job.JobId, -1, segments);

        job.IsFfmpegRunning.Should().BeTrue();
        job.LastClientMediaSegmentRequest.Should().Be(1055);
        job.TargetSegmentIndex.Should().Be(target);
        job.HasObservedReadyOutput.Should().BeFalse();
    }

    private static List<HlsSegment> CreateSegments(int count)
    {
        var segments = new List<HlsSegment>(count);
        for (var i = 0; i < count; i++)
        {
            segments.Add(new HlsSegment
            {
                Number = i,
                StartTimestamp = i * 6000,
                Duration = 6000
            });
        }

        return segments;
    }
}
