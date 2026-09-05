using System.Collections.Concurrent;
using AwesomeAssertions;
using K7.Server.Application.Common;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Services;

public class BackgroundTaskCandidateSelectorTests
{
    private static Dictionary<BackgroundTaskLane, int> DefaultLimits(int metadataCeiling = 8) => new()
    {
        [BackgroundTaskLane.Metadata] = metadataCeiling,
        [BackgroundTaskLane.Probe] = 4,
        [BackgroundTaskLane.Federation] = 1
    };

    [Test]
    public void IsEligibleForCandidateWindow_ShouldExcludeSaturatedMetadataProvider()
    {
        var active = new ConcurrentDictionary<string, int>(StringComparer.Ordinal)
        {
            [BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tvdb)] =
                BackgroundTaskScheduling.MetadataProviderLimit
        };
        var saturation = BackgroundTaskCandidateSelector.BuildSaturation(active, DefaultLimits());

        var tvdb = new BackgroundTaskPickCandidate(Guid.NewGuid(), BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tvdb);
        var tmdb = new BackgroundTaskPickCandidate(Guid.NewGuid(), BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tmdb);
        var probe = new BackgroundTaskPickCandidate(Guid.NewGuid(), BackgroundTaskLane.Probe, null, null);

        BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(tvdb, saturation).Should().BeFalse();
        BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(tmdb, saturation).Should().BeTrue();
        BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(probe, saturation).Should().BeTrue();
    }

    [Test]
    public void IsEligibleForCandidateWindow_ShouldExcludeAllMetadata_WhenCeilingReached()
    {
        var active = new ConcurrentDictionary<string, int>(StringComparer.Ordinal)
        {
            [BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tvdb)] = 1,
            [BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tmdb)] = 1
        };
        var saturation = BackgroundTaskCandidateSelector.BuildSaturation(active, DefaultLimits(metadataCeiling: 2));

        saturation.MetadataCeilingHit.Should().BeTrue();

        var mb = new BackgroundTaskPickCandidate(Guid.NewGuid(), BackgroundTaskLane.Metadata, null, MetadataProviderNames.MusicBrainz);
        var probe = new BackgroundTaskPickCandidate(Guid.NewGuid(), BackgroundTaskLane.Probe, null, null);

        BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(mb, saturation).Should().BeFalse();
        BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(probe, saturation).Should().BeTrue();
    }

    [Test]
    public void IsEligibleForCandidateWindow_ShouldExcludeSaturatedFederationPeer()
    {
        var peerA = Guid.NewGuid();
        var peerB = Guid.NewGuid();
        var active = new ConcurrentDictionary<string, int>(StringComparer.Ordinal)
        {
            [BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Federation, peerA)] = 1
        };
        var saturation = BackgroundTaskCandidateSelector.BuildSaturation(active, DefaultLimits());

        var taskA = new BackgroundTaskPickCandidate(Guid.NewGuid(), BackgroundTaskLane.Federation, peerA, null);
        var taskB = new BackgroundTaskPickCandidate(Guid.NewGuid(), BackgroundTaskLane.Federation, peerB, null);

        BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(taskA, saturation).Should().BeFalse();
        BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(taskB, saturation).Should().BeTrue();
    }

    [Test]
    public void IsEligibleForCandidateWindow_ShouldExcludeSaturatedPlainLane()
    {
        var active = new ConcurrentDictionary<string, int>(StringComparer.Ordinal)
        {
            [BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Probe, null)] = 4
        };
        var limits = DefaultLimits();
        limits[BackgroundTaskLane.Probe] = 4;
        var saturation = BackgroundTaskCandidateSelector.BuildSaturation(active, limits);

        var probe = new BackgroundTaskPickCandidate(Guid.NewGuid(), BackgroundTaskLane.Probe, null, null);
        var metadata = new BackgroundTaskPickCandidate(Guid.NewGuid(), BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tmdb);

        BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(probe, saturation).Should().BeFalse();
        BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(metadata, saturation).Should().BeTrue();
    }

    [Test]
    public void TryAcquireNext_ShouldSpillOverToLowerWorkClass_WhenPreferredProviderIsSaturated()
    {
        // CriticalEnrich on tvdb is already in-flight; Polish on tmdb must still be picked
        // instead of leaving the worker idle behind the preferred WorkClass head.
        var active = new ConcurrentDictionary<string, int>(StringComparer.Ordinal)
        {
            [BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tvdb)] =
                BackgroundTaskScheduling.MetadataProviderLimit
        };
        var limits = DefaultLimits();
        var saturation = BackgroundTaskCandidateSelector.BuildSaturation(active, limits);

        var criticalTvdb = new BackgroundTaskPickCandidate(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            BackgroundTaskLane.Metadata,
            null,
            MetadataProviderNames.Tvdb);
        var polishTmdb = new BackgroundTaskPickCandidate(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            BackgroundTaskLane.Metadata,
            null,
            MetadataProviderNames.Tmdb);

        // Window is already WorkClass-ordered (Critical then Polish), as the EF query would return.
        var window = new[] { criticalTvdb, polishTmdb }
            .Where(c => BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(c, saturation))
            .ToList();

        window.Should().ContainSingle(c => c.Id == polishTmdb.Id);

        var selected = BackgroundTaskCandidateSelector.TryAcquireNext(
            window,
            active,
            limits,
            saturation,
            out var acquiredKey);

        selected.Should().NotBeNull();
        selected!.Value.Id.Should().Be(polishTmdb.Id);
        acquiredKey.Should().Be("Metadata:tmdb");
        active["Metadata:tmdb"].Should().Be(1);
    }

    [Test]
    public void TryAcquireNext_ShouldSpillOverToProbe_WhenMetadataCeilingIsFull()
    {
        var active = new ConcurrentDictionary<string, int>(StringComparer.Ordinal)
        {
            [BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tvdb)] = 1,
            [BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tmdb)] = 1
        };
        var limits = DefaultLimits(metadataCeiling: 2);
        var saturation = BackgroundTaskCandidateSelector.BuildSaturation(active, limits);

        var enrich = new BackgroundTaskPickCandidate(
            Guid.NewGuid(),
            BackgroundTaskLane.Metadata,
            null,
            MetadataProviderNames.MusicBrainz);
        var probe = new BackgroundTaskPickCandidate(
            Guid.NewGuid(),
            BackgroundTaskLane.Probe,
            null,
            null);

        var window = new[] { enrich, probe }
            .Where(c => BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(c, saturation))
            .ToList();

        window.Should().ContainSingle(c => c.Id == probe.Id);

        var selected = BackgroundTaskCandidateSelector.TryAcquireNext(
            window,
            active,
            limits,
            saturation,
            out var acquiredKey);

        selected.Should().NotBeNull();
        selected!.Value.Id.Should().Be(probe.Id);
        acquiredKey.Should().Be(nameof(BackgroundTaskLane.Probe));
    }

    [Test]
    public void TryAcquireNext_ShouldSkipHeadAndTakeNext_WhenGateRejectsBetweenQueryAndAcquire()
    {
        // Candidate window still contains tvdb (filter saw it free), but another worker took the
        // slot before TryAcquire - spillover must continue to tmdb in the same window.
        var active = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var limits = DefaultLimits();
        var saturation = BackgroundTaskCandidateSelector.BuildSaturation(active, limits);

        var tvdb = new BackgroundTaskPickCandidate(
            Guid.NewGuid(),
            BackgroundTaskLane.Metadata,
            null,
            MetadataProviderNames.Tvdb);
        var tmdb = new BackgroundTaskPickCandidate(
            Guid.NewGuid(),
            BackgroundTaskLane.Metadata,
            null,
            MetadataProviderNames.Tmdb);

        // Simulate a race: slot taken after BuildSaturation / filter.
        active[BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tvdb)] =
            BackgroundTaskScheduling.MetadataProviderLimit;

        var selected = BackgroundTaskCandidateSelector.TryAcquireNext(
            [tvdb, tmdb],
            active,
            limits,
            saturation,
            out var acquiredKey);

        selected.Should().NotBeNull();
        selected!.Value.Id.Should().Be(tmdb.Id);
        acquiredKey.Should().Be("Metadata:tmdb");
    }

    [Test]
    public void TryAcquireNext_ShouldSpillOver_WhenPreferredProviderIsCoolingDown()
    {
        var active = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var limits = DefaultLimits();
        var cooling = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { MetadataProviderNames.Tvdb };
        var saturation = BackgroundTaskCandidateSelector.BuildSaturation(active, limits, cooling);

        var criticalTvdb = new BackgroundTaskPickCandidate(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            BackgroundTaskLane.Metadata,
            null,
            MetadataProviderNames.Tvdb);
        var polishTmdb = new BackgroundTaskPickCandidate(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            BackgroundTaskLane.Metadata,
            null,
            MetadataProviderNames.Tmdb);

        var window = new[] { criticalTvdb, polishTmdb }
            .Where(c => BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(c, saturation))
            .ToList();

        window.Should().ContainSingle(c => c.Id == polishTmdb.Id);

        var selected = BackgroundTaskCandidateSelector.TryAcquireNext(
            window,
            active,
            limits,
            saturation,
            out var acquiredKey);

        selected.Should().NotBeNull();
        selected!.Value.Id.Should().Be(polishTmdb.Id);
        acquiredKey.Should().Be("Metadata:tmdb");
    }

    [Test]
    public void TryAcquireNext_ShouldReturnNull_WhenEveryCandidateIsBlocked()
    {
        var active = new ConcurrentDictionary<string, int>(StringComparer.Ordinal)
        {
            [BackgroundTaskConcurrencyGate.BuildKey(BackgroundTaskLane.Metadata, null, MetadataProviderNames.Tvdb)] =
                BackgroundTaskScheduling.MetadataProviderLimit
        };
        var limits = DefaultLimits(metadataCeiling: 1);
        var saturation = BackgroundTaskCandidateSelector.BuildSaturation(active, limits);

        var onlyTvdb = new BackgroundTaskPickCandidate(
            Guid.NewGuid(),
            BackgroundTaskLane.Metadata,
            null,
            MetadataProviderNames.Tvdb);

        var window = new[] { onlyTvdb }
            .Where(c => BackgroundTaskCandidateSelector.IsEligibleForCandidateWindow(c, saturation))
            .ToList();

        window.Should().BeEmpty();

        var selected = BackgroundTaskCandidateSelector.TryAcquireNext(
            [onlyTvdb],
            active,
            limits,
            saturation,
            out var acquiredKey);

        selected.Should().BeNull();
        acquiredKey.Should().BeNull();
    }
}
