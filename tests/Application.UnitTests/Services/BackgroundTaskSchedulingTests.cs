using FluentAssertions;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Services;

public class BackgroundTaskSchedulingTests
{
    [Test]
    public void GetDefaultLimit_ShouldAllowParallelism_WhenLaneIsProbe()
    {
        // The probes of a scan batch must be able to occupy every worker, otherwise media creation
        // tasks of the same batch get a worker first and medias become visible before being playable.
        BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.Probe)
            .Should().BeGreaterThanOrEqualTo(BackgroundTaskScheduling.DefaultWorkerCount);
    }

    [Test]
    public void GetDefaultLimit_ShouldReturnOne_WhenLaneIsCpuBound()
    {
        BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.FfmpegPrepare).Should().Be(1);
        BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.MediaAnalysis).Should().Be(1);
        BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.ImageExtract).Should().Be(1);
        BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.LibraryScan).Should().Be(1);
        BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.DownloadTranscode).Should().Be(1);
    }

    [Test]
    public void GetDefaultLimit_ShouldReturnDefaultLaneLimit_WhenLaneHasNoOverride()
    {
        BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.Federation)
            .Should().Be(BackgroundTaskScheduling.DefaultLaneLimit);
    }

    [Test]
    public void WorkClass_ShouldOrderCriticalPathAboveBackgroundWork()
    {
        // Values ARE the scheduling weights: critical path first, polish last.
        ((int)BackgroundTaskWorkClass.CriticalLink).Should().BeGreaterThan((int)BackgroundTaskWorkClass.CriticalEnrich);
        ((int)BackgroundTaskWorkClass.CriticalEnrich).Should().BeGreaterThan((int)BackgroundTaskWorkClass.Prepare);
        ((int)BackgroundTaskWorkClass.Prepare).Should().BeGreaterThan((int)BackgroundTaskWorkClass.Polish);
    }

    [Test]
    public void WorkClass_ShouldRankProbeAboveLink()
    {
        // Deliberate: the probes of a scan batch must drain before the media creation tasks of the same
        // batch obtain a worker. The reverse order lets a media become visible while its probe is still
        // queued, which is the ghost-page state this scheduling exists to avoid.
        ((int)BackgroundTaskWorkClass.CriticalProbe).Should().BeGreaterThan((int)BackgroundTaskWorkClass.CriticalLink);
    }

    [Test]
    public void WorkClass_ShouldHaveDistinctValues()
    {
        // Two members sharing a value would make ToString() ambiguous and break the admin labels, which
        // resolve their resource keys from the enum name.
        var values = Enum.GetValues<BackgroundTaskWorkClass>().Select(workClass => (int)workClass).ToList();

        values.Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void ClampWorkerCount_ShouldAllowZero_ToPauseAllWorkers()
    {
        BackgroundTaskScheduling.ClampWorkerCount(0).Should().Be(0);
        BackgroundTaskScheduling.ClampWorkerCount(-3).Should().Be(0);
        BackgroundTaskScheduling.ClampWorkerCount(1).Should().Be(1);
        BackgroundTaskScheduling.ClampWorkerCount(BackgroundTaskScheduling.MaxWorkerCount + 5)
            .Should().Be(BackgroundTaskScheduling.MaxWorkerCount);
    }

    [Test]
    public void ClampLaneLimit_ShouldAllowZero_ToPauseLane()
    {
        BackgroundTaskScheduling.ClampLaneLimit(0).Should().Be(0);
        BackgroundTaskScheduling.ClampLaneLimit(-1).Should().Be(0);
        BackgroundTaskScheduling.ClampLaneLimit(BackgroundTaskScheduling.MaxLaneLimit + 1)
            .Should().Be(BackgroundTaskScheduling.MaxLaneLimit);
    }
}
