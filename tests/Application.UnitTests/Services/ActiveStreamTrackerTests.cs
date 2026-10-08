using AwesomeAssertions;
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

    [Test]
    public void Upsert_ShouldKeepConcurrentPlays_WhenSameUserAndMediaOnDifferentDevices()
    {
        var tracker = new ActiveStreamTracker();
        var userId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var firstSession = Guid.NewGuid();
        var secondSession = Guid.NewGuid();
        var tvId = Guid.NewGuid();
        var browserId = Guid.NewGuid();

        tracker.Upsert(firstSession, CreateStream(firstSession, userId, mediaId, tvId, "Living room"));
        tracker.Upsert(secondSession, CreateStream(secondSession, userId, mediaId, browserId, "Chrome"));

        var active = tracker.GetActiveStreams();
        active.Should().HaveCount(2);
        active.Select(s => s.SessionId).Should().BeEquivalentTo([firstSession, secondSession]);
    }

    [Test]
    public void Upsert_ShouldReplaceRestart_WhenSameUserMediaAndDevice()
    {
        var tracker = new ActiveStreamTracker();
        var userId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var firstSession = Guid.NewGuid();
        var restartedSession = Guid.NewGuid();

        tracker.Upsert(firstSession, CreateStream(firstSession, userId, mediaId, deviceId, "TV"));
        tracker.Upsert(restartedSession, CreateStream(restartedSession, userId, mediaId, deviceId, "TV"));

        var active = tracker.GetActiveStreams();
        active.Should().ContainSingle(s => s.SessionId == restartedSession);
        tracker.GetStreamInfo(firstSession).Should().BeNull();
    }

    [Test]
    public void Upsert_ShouldKeepBoth_WhenDeviceIdsDifferEvenIfNamesMatch()
    {
        var tracker = new ActiveStreamTracker();
        var userId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var firstSession = Guid.NewGuid();
        var secondSession = Guid.NewGuid();

        tracker.Upsert(firstSession, CreateStream(firstSession, userId, mediaId, Guid.NewGuid(), "K7"));
        tracker.Upsert(secondSession, CreateStream(secondSession, userId, mediaId, Guid.NewGuid(), "K7"));

        tracker.GetActiveStreams().Should().HaveCount(2);
    }

    [Test]
    public void Upsert_ShouldReplaceOtherMedia_WhenTheSameDeviceStartsSomethingElse()
    {
        var tracker = new ActiveStreamTracker();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var movieSession = Guid.NewGuid();
        var musicSession = Guid.NewGuid();

        tracker.Upsert(movieSession, CreateStream(movieSession, userId, Guid.NewGuid(), deviceId, "Browser"));
        tracker.Upsert(musicSession, CreateStream(musicSession, userId, Guid.NewGuid(), deviceId, "Browser"));

        var active = tracker.GetActiveStreams();
        active.Should().ContainSingle(s => s.SessionId == musicSession);
        tracker.GetStreamInfo(movieSession).Should().BeNull();
    }

    [Test]
    public void ReleaseTakenOver_ShouldDropTheMediaOnEveryDeviceExceptTheOneThatResumed()
    {
        var tracker = new ActiveStreamTracker();
        var userId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var stolenDevice = Guid.NewGuid();
        var resumedDevice = Guid.NewGuid();
        var otherDevice = Guid.NewGuid();
        var otherMedia = Guid.NewGuid();

        tracker.Upsert(Guid.NewGuid(), CreateStream(Guid.NewGuid(), userId, mediaId, stolenDevice, "Phone"));
        tracker.Upsert(Guid.NewGuid(), CreateStream(Guid.NewGuid(), userId, mediaId, resumedDevice, "Browser"));
        tracker.Upsert(Guid.NewGuid(), CreateStream(Guid.NewGuid(), userId, otherMedia, otherDevice, "TV"));

        var released = tracker.ReleaseTakenOver("user-1", mediaId, null, resumedDevice);

        released.Should().Equal(stolenDevice);
        tracker.GetActiveStreams().Select(s => s.DeviceId).Should().BeEquivalentTo([resumedDevice, otherDevice]);
    }

    [Test]
    public void ReleaseDevice_ShouldDropOnlyThatDevice()
    {
        var tracker = new ActiveStreamTracker();
        var userId = Guid.NewGuid();
        var closedDevice = Guid.NewGuid();
        var otherDevice = Guid.NewGuid();

        tracker.Upsert(Guid.NewGuid(), CreateStream(Guid.NewGuid(), userId, Guid.NewGuid(), closedDevice, "Windows"));
        tracker.Upsert(Guid.NewGuid(), CreateStream(Guid.NewGuid(), userId, Guid.NewGuid(), otherDevice, "Phone"));

        var identities = tracker.ReleaseDevice(closedDevice);

        identities.Should().Equal("user-1");
        tracker.GetActiveStreams().Select(s => s.DeviceId).Should().BeEquivalentTo([otherDevice]);
    }

    [Test]
    public void Upsert_ShouldKeepIndexedFileId_WhenLaterUpdateOmitsIt()
    {
        var tracker = new ActiveStreamTracker();
        var sessionId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        tracker.Upsert(sessionId, CreateStream(sessionId, userId, mediaId, deviceId, "TV") with
        {
            IndexedFileId = fileId
        });
        tracker.Upsert(sessionId, CreateStream(sessionId, userId, mediaId, deviceId, "TV"));

        tracker.GetStreamInfo(sessionId)!.IndexedFileId.Should().Be(fileId);
    }

    [Test]
    public void Upsert_ShouldKeepFederatedPlaybackExecution_WhenLaterUpdateOmitsIt()
    {
        var tracker = new ActiveStreamTracker();
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        tracker.Upsert(sessionId, new ActiveStreamInfo
        {
            SessionId = sessionId,
            IdentityUserId = "user-1",
            UserId = userId,
            DeviceId = deviceId,
            DeviceName = "Browser",
            DeviceType = nameof(ClientType.Web),
            FederatedPlaybackExecution = FederatedPlaybackExecution.Peer,
            StartedAt = DateTime.UtcNow,
            Position = 0,
            Duration = 100,
            State = (int)PlaybackState.Playing
        });

        // Progress updates often fill MediaId later and omit federation fields.
        tracker.Upsert(sessionId, CreateStream(sessionId, userId, mediaId, deviceId, "Browser"));

        var info = tracker.GetStreamInfo(sessionId)!;
        info.FederatedPlaybackExecution.Should().Be(FederatedPlaybackExecution.Peer);
        info.MediaId.Should().Be(mediaId);
    }

    private static ActiveStreamInfo CreateStream(
        Guid sessionId,
        Guid userId,
        Guid mediaId,
        Guid? deviceId,
        string deviceName) =>
        new()
        {
            SessionId = sessionId,
            IdentityUserId = "user-1",
            UserId = userId,
            MediaId = mediaId,
            MediaTitle = "Movie",
            DeviceId = deviceId,
            DeviceName = deviceName,
            DeviceClient = nameof(ClientType.Native),
            StartedAt = DateTime.UtcNow,
            Position = 0,
            Duration = 100,
            State = (int)PlaybackState.Playing
        };
}
