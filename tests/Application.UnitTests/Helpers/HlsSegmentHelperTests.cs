using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Helpers;
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
        ((ComputeHlsSegmentsCommand)captured.Request).SegmentsDuration.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Test]
    public void FallbackTranscodingVideoCodec_ShouldBeH264()
    {
        HlsSegmentHelper.FallbackTranscodingVideoCodec.Should().Be("h264");
    }
}
