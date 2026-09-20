using AwesomeAssertions;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public sealed class NowPlayingMapperTests
{
    [Test]
    public void FromTracker_ShouldReturnOnlyCallerSessions()
    {
        var tracker = new ActiveStreamTracker();
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();

        tracker.Upsert(mine, CreateStream(mine, "user-a", userId, mediaId));
        tracker.Upsert(other, CreateStream(other, "user-b", userId, mediaId));

        var sessions = NowPlayingMapper.FromTracker(tracker, "user-a");

        sessions.Should().ContainSingle(s => s.SessionId == mine);
    }

    [Test]
    public void FromStream_ShouldDisableControl_ForExternalClients()
    {
        var stream = CreateStream(Guid.NewGuid(), "user-a", Guid.NewGuid(), Guid.NewGuid()) with
        {
            DeviceClient = nameof(ClientType.External),
            DeviceId = Guid.NewGuid()
        };

        var dto = NowPlayingMapper.FromStream(stream);

        dto.CanControl.Should().BeFalse();
        dto.IsAudio.Should().BeFalse();
    }

    [Test]
    public void FromStream_ShouldMarkAudio_ForMusicTrack()
    {
        var stream = CreateStream(Guid.NewGuid(), "user-a", Guid.NewGuid(), Guid.NewGuid()) with
        {
            MediaType = nameof(MediaType.MusicTrack)
        };

        NowPlayingMapper.FromStream(stream).IsAudio.Should().BeTrue();
    }

    private static ActiveStreamInfo CreateStream(
        Guid sessionId,
        string identityUserId,
        Guid userId,
        Guid mediaId) =>
        new()
        {
            SessionId = sessionId,
            IdentityUserId = identityUserId,
            UserId = userId,
            MediaId = mediaId,
            MediaTitle = "Movie",
            MediaType = nameof(MediaType.Movie),
            DeviceId = Guid.NewGuid(),
            DeviceName = "TV",
            DeviceClient = nameof(ClientType.Native),
            StartedAt = DateTime.UtcNow,
            Position = 10,
            Duration = 100,
            State = (int)PlaybackState.Playing,
            PlaybackRate = 1
        };
}
