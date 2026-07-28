using System.Collections.Concurrent;
using FluentAssertions;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Services;

public class BackgroundTaskConcurrencyGateTests
{
    [Test]
    public void TryAcquire_ShouldAllowUpToLimit()
    {
        var counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var key = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.LibraryScan, null);

        BackgroundTaskConcurrencyGate.TryAcquire(counts, key, limit: 1).Should().BeTrue();
        BackgroundTaskConcurrencyGate.TryAcquire(counts, key, limit: 1).Should().BeFalse();
        counts[key].Should().Be(1);

        BackgroundTaskConcurrencyGate.Release(counts, key);
        counts[key].Should().Be(0);

        BackgroundTaskConcurrencyGate.TryAcquire(counts, key, limit: 1).Should().BeTrue();
    }

    [Test]
    public void TryAcquire_ShouldRespectHigherLimit()
    {
        var counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var key = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Probe, null);

        BackgroundTaskConcurrencyGate.TryAcquire(counts, key, limit: 2).Should().BeTrue();
        BackgroundTaskConcurrencyGate.TryAcquire(counts, key, limit: 2).Should().BeTrue();
        BackgroundTaskConcurrencyGate.TryAcquire(counts, key, limit: 2).Should().BeFalse();
        counts[key].Should().Be(2);
    }

    [Test]
    public void TryAcquire_ShouldSerializeParallelAcquires_WhenLimitIsOne()
    {
        var counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var key = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.LibraryScan, null);
        var acquired = 0;

        Parallel.For(0, 32, _ =>
        {
            if (BackgroundTaskConcurrencyGate.TryAcquire(counts, key, limit: 1))
                Interlocked.Increment(ref acquired);
        });

        acquired.Should().Be(1);
        counts[key].Should().Be(1);
    }

    [Test]
    public void BuildKey_ShouldIsolateFederationPeers()
    {
        var peerA = Guid.NewGuid();
        var peerB = Guid.NewGuid();

        var keyA = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Federation, peerA);
        var keyB = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Federation, peerB);

        keyA.Should().NotBe(keyB);

        // One slow peer must not consume the slot of another peer.
        var counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        BackgroundTaskConcurrencyGate.TryAcquire(counts, keyA, limit: 1).Should().BeTrue();
        BackgroundTaskConcurrencyGate.TryAcquire(counts, keyB, limit: 1).Should().BeTrue();
    }

    [Test]
    public void BuildKey_ShouldIgnorePeerId_WhenLaneIsNotFederation()
    {
        var key = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Probe, Guid.NewGuid());
        key.Should().Be(BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Probe, null));
    }
}
