using System.Collections.Concurrent;
using FluentAssertions;
using K7.Server.Application.Common;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
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

    [Test]
    public void BuildKey_ShouldIsolateMetadataProviders()
    {
        var tvdb = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tvdb);
        var tmdb = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tmdb);

        tvdb.Should().Be("Metadata:tvdb");
        tmdb.Should().Be("Metadata:tmdb");
        tvdb.Should().NotBe(tmdb);
    }

    [Test]
    public void TryAcquire_ShouldLimitEachMetadataProvider_AndRespectCeiling()
    {
        var counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var limits = new Dictionary<BackgroundTaskLane, int>
        {
            [BackgroundTaskLane.Metadata] = 2
        };

        var tvdb = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tvdb);
        var tmdb = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tmdb);
        var mb = BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.MusicBrainz);

        BackgroundTaskConcurrencyGate.TryAcquire(counts, tvdb, BackgroundTaskScheduling.MetadataProviderLimit, limits)
            .Should().BeTrue();
        BackgroundTaskConcurrencyGate.TryAcquire(counts, tvdb, BackgroundTaskScheduling.MetadataProviderLimit, limits)
            .Should().BeTrue();
        BackgroundTaskConcurrencyGate.TryAcquire(counts, tvdb, BackgroundTaskScheduling.MetadataProviderLimit, limits)
            .Should().BeFalse();

        // Lane ceiling is already full (2), so another provider cannot acquire.
        BackgroundTaskConcurrencyGate.TryAcquire(counts, tmdb, BackgroundTaskScheduling.MetadataProviderLimit, limits)
            .Should().BeFalse();
        BackgroundTaskConcurrencyGate.TryAcquire(counts, mb, BackgroundTaskScheduling.MetadataProviderLimit, limits)
            .Should().BeFalse();

        BackgroundTaskConcurrencyGate.CountMetadataActive(counts).Should().Be(2);
    }
}
