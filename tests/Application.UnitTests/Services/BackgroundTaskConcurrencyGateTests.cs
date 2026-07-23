using System.Collections.Concurrent;
using FluentAssertions;
using K7.Server.Application.Services;

namespace K7.Server.Application.UnitTests.Services;

public class BackgroundTaskConcurrencyGateTests
{
    [Test]
    public void TryAcquire_ShouldAllowUpToLimit()
    {
        var counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        const string group = "library-scan";

        BackgroundTaskConcurrencyGate.TryAcquire(counts, group, limit: 1).Should().BeTrue();
        BackgroundTaskConcurrencyGate.TryAcquire(counts, group, limit: 1).Should().BeFalse();
        counts[group].Should().Be(1);

        BackgroundTaskConcurrencyGate.Release(counts, group);
        counts[group].Should().Be(0);

        BackgroundTaskConcurrencyGate.TryAcquire(counts, group, limit: 1).Should().BeTrue();
    }

    [Test]
    public void TryAcquire_ShouldAllowNullGroup()
    {
        var counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        BackgroundTaskConcurrencyGate.TryAcquire(counts, null, limit: 1).Should().BeTrue();
        counts.Should().BeEmpty();
    }

    [Test]
    public void TryAcquire_ShouldRespectHigherLimit()
    {
        var counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        const string group = "ffmpeg";

        BackgroundTaskConcurrencyGate.TryAcquire(counts, group, limit: 2).Should().BeTrue();
        BackgroundTaskConcurrencyGate.TryAcquire(counts, group, limit: 2).Should().BeTrue();
        BackgroundTaskConcurrencyGate.TryAcquire(counts, group, limit: 2).Should().BeFalse();
        counts[group].Should().Be(2);
    }

    [Test]
    public void TryAcquire_ShouldSerializeParallelAcquires_WhenLimitIsOne()
    {
        var counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        const string group = "library-scan";
        var acquired = 0;

        Parallel.For(0, 32, _ =>
        {
            if (BackgroundTaskConcurrencyGate.TryAcquire(counts, group, limit: 1))
                Interlocked.Increment(ref acquired);
        });

        acquired.Should().Be(1);
        counts[group].Should().Be(1);
    }
}
