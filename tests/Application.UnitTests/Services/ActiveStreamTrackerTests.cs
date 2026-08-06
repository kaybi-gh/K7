using FluentAssertions;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class ActiveStreamTrackerTests
{
    [Test]
    public void GetActiveStreams_ShouldKeepExternalPlayingAlive_WithoutEstimatingPosition()
    {
        var tracker = new ActiveStreamTracker();
        var sessionId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddMinutes(-3);

        tracker.Upsert(sessionId, new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = "user-1",
            UserId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            MediaTitle = "Track",
            MediaType = nameof(MediaType.MusicTrack),
            DeviceClient = nameof(ClientType.External),
            StartedAt = startedAt,
            Position = 0,
            Duration = 240,
            State = (int)PlaybackState.Playing
        });

        // Force LastUpdatedAt into the stale window, then refresh via GetActiveStreams.
        var info = tracker.GetStreamInfo(sessionId)!;
        info.LastUpdatedAt = DateTime.UtcNow.AddSeconds(-120);

        var active = tracker.GetActiveStreams();

        active.Should().ContainSingle(s => s.SessionId == sessionId);
        active[0].Position.Should().Be(0);
        active[0].LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Test]
    public void GetActiveStreams_ShouldEstimatePosition_WhenHasPlaybackProgress()
    {
        var tracker = new ActiveStreamTracker();
        var sessionId = Guid.NewGuid();

        tracker.Upsert(sessionId, new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = "user-1",
            UserId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            MediaTitle = "Track",
            MediaType = nameof(MediaType.MusicTrack),
            DeviceClient = nameof(ClientType.External),
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            Position = 30,
            Duration = 240,
            State = (int)PlaybackState.Playing,
            HasPlaybackProgress = true,
            PlaybackRate = 1.0
        });

        var info = tracker.GetStreamInfo(sessionId)!;
        info.LastUpdatedAt = DateTime.UtcNow.AddSeconds(-10);

        var active = tracker.GetActiveStreams();

        active.Should().ContainSingle();
        active[0].Position.Should().BeApproximately(40, 1.5);
    }

    [Test]
    public void Upsert_ShouldNotReplaceDifferentMedia_WhenCallerSkips()
    {
        // Guardrail for OpenSubsonic prefetch behavior: same session id, different media must be
        // handled by the caller; tracker itself replaces on Upsert.
        var tracker = new ActiveStreamTracker();
        var sessionId = Guid.NewGuid();
        var firstMedia = Guid.NewGuid();
        var secondMedia = Guid.NewGuid();

        tracker.Upsert(sessionId, new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = "user-1",
            MediaId = firstMedia,
            StartedAt = DateTime.UtcNow,
            Position = 0,
            Duration = 100,
            State = (int)PlaybackState.Playing
        });

        tracker.Upsert(sessionId, new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = "user-1",
            MediaId = secondMedia,
            StartedAt = DateTime.UtcNow,
            Position = 0,
            Duration = 100,
            State = (int)PlaybackState.Playing
        });

        tracker.GetStreamInfo(sessionId)!.MediaId.Should().Be(secondMedia);
    }

    [Test]
    public void EndOpenSubsonicTransfer_ShouldPromotePending_WhenCurrentTransferEnds()
    {
        var tracker = new ActiveStreamTracker();
        var sessionId = Guid.NewGuid();
        var mediaA = Guid.NewGuid();
        var mediaB = Guid.NewGuid();

        tracker.Upsert(sessionId, new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = "user-1",
            MediaId = mediaA,
            MediaTitle = "A",
            StartedAt = DateTime.UtcNow,
            Position = 0,
            Duration = 100,
            State = (int)PlaybackState.Playing,
            DeviceClient = nameof(ClientType.External)
        });
        tracker.BeginOpenSubsonicTransfer(sessionId, mediaA);

        tracker.SetOpenSubsonicPending(sessionId, new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = "user-1",
            MediaId = mediaB,
            MediaTitle = "B",
            StartedAt = DateTime.UtcNow,
            Position = 0,
            Duration = 120,
            State = (int)PlaybackState.Playing,
            DeviceClient = nameof(ClientType.External)
        });

        tracker.EndOpenSubsonicTransfer(sessionId, mediaA);

        var info = tracker.GetStreamInfo(sessionId)!;
        info.MediaId.Should().Be(mediaB);
        info.MediaTitle.Should().Be("B");
    }

    [Test]
    public void EndOpenSubsonicTransfer_ShouldNotPromote_WhileCurrentTransferStillActive()
    {
        var tracker = new ActiveStreamTracker();
        var sessionId = Guid.NewGuid();
        var mediaA = Guid.NewGuid();
        var mediaB = Guid.NewGuid();

        tracker.Upsert(sessionId, new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = "user-1",
            MediaId = mediaA,
            MediaTitle = "A",
            StartedAt = DateTime.UtcNow,
            Position = 0,
            Duration = 100,
            State = (int)PlaybackState.Playing
        });
        tracker.BeginOpenSubsonicTransfer(sessionId, mediaA);
        tracker.BeginOpenSubsonicTransfer(sessionId, mediaA);

        tracker.SetOpenSubsonicPending(sessionId, new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = "user-1",
            MediaId = mediaB,
            MediaTitle = "B",
            StartedAt = DateTime.UtcNow,
            Position = 0,
            Duration = 120,
            State = (int)PlaybackState.Playing
        });

        tracker.EndOpenSubsonicTransfer(sessionId, mediaA);
        tracker.GetStreamInfo(sessionId)!.MediaId.Should().Be(mediaA);

        tracker.EndOpenSubsonicTransfer(sessionId, mediaA);
        tracker.GetStreamInfo(sessionId)!.MediaId.Should().Be(mediaB);
    }
}
