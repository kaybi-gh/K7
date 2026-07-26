using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class HlsSegmentHelperTests
{
    [Test]
    public async Task QueueSegmentComputationIfMissingAsync_ShouldEnqueueHighPriorityFfmpegTask()
    {
        var sender = Substitute.For<ISender>();
        var logger = Substitute.For<ILogger>();
        var indexedFileId = Guid.NewGuid();
        CreateBackgroundTaskCommand? captured = null;
        sender.Send(Arg.Do<CreateBackgroundTaskCommand>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        await HlsSegmentHelper.QueueSegmentComputationIfMissingAsync(
            sender, indexedFileId, logger, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Priority.Should().Be(BackgroundTaskPriority.High);
        captured.TargetEntityId.Should().Be(indexedFileId);
        captured.ConcurrencyGroup.Should().Be("ffmpeg");
        captured.Request.Should().BeOfType<ComputeHlsSegmentsCommand>();
        ((ComputeHlsSegmentsCommand)captured.Request).Id.Should().Be(indexedFileId);
        ((ComputeHlsSegmentsCommand)captured.Request).SegmentsDuration.Should().Be(
            TimeSpan.FromMilliseconds(HlsSegmentHelper.TargetSegmentDurationMs));
    }

    [Test]
    public void AlignToPreviousSegmentBoundary_ShouldFloorToEqualLengthFallbackGrid()
    {
        HlsSegmentHelper.AlignToPreviousSegmentBoundary(0).Should().Be(0);
        HlsSegmentHelper.AlignToPreviousSegmentBoundary(5.9).Should().Be(0);
        HlsSegmentHelper.AlignToPreviousSegmentBoundary(6).Should().Be(6);
        HlsSegmentHelper.AlignToPreviousSegmentBoundary(13.2).Should().Be(12);
    }

    [Test]
    public void AlignToPreviousSegmentBoundary_ShouldUseKeyframeDurations()
    {
        double[] durations = [2.0, 4.5, 5.5, 6.0];
        HlsSegmentHelper.AlignToPreviousSegmentBoundary(0, durations).Should().Be(0);
        HlsSegmentHelper.AlignToPreviousSegmentBoundary(1.5, durations).Should().Be(0);
        HlsSegmentHelper.AlignToPreviousSegmentBoundary(2.0, durations).Should().Be(2.0);
        HlsSegmentHelper.AlignToPreviousSegmentBoundary(2.1, durations).Should().Be(2.0);
        HlsSegmentHelper.AlignToPreviousSegmentBoundary(7.0, durations).Should().Be(6.5);
    }

    [Test]
    public void ResolveVideoStreamingSegments_ShouldPreferKeyframeRows()
    {
        var keyframes = new List<HlsSegment>
        {
            new() { Number = 1, StartTimestamp = 2000, Duration = 4000 },
            new() { Number = 0, StartTimestamp = 0, Duration = 2000 }
        };

        var resolved = HlsSegmentHelper.ResolveVideoStreamingSegments(keyframes, 6000);
        resolved.Select(s => s.Number).Should().Equal(0, 1);
        resolved.Select(s => s.Duration).Should().Equal(2000L, 4000L);
    }

    [Test]
    public void ResolveVideoStreamingSegments_ShouldFallbackToEqualLength()
    {
        var resolved = HlsSegmentHelper.ResolveVideoStreamingSegments([], 13000);
        resolved.Should().HaveCount(3);
        resolved.Select(s => s.Duration).Should().Equal(6000L, 6000L, 1000L);
    }

    [Test]
    public void FallbackTranscodingVideoCodec_ShouldBeH264()
    {
        HlsSegmentHelper.FallbackTranscodingVideoCodec.Should().Be("h264");
    }
}
