using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.Scrobbling;

[TestFixture]
public class ScrobbleDispatcherTests
{
    [Test]
    public void CompletionGate_ShouldAllowOnlyOneClaimPerSession()
    {
        var gate = new ScrobbleCompletionGate();
        var sessionId = Guid.NewGuid();

        gate.TryClaim(sessionId).Should().BeTrue();
        gate.TryClaim(sessionId).Should().BeFalse();
        gate.TryClaim(Guid.NewGuid()).Should().BeTrue();
    }

    [Test]
    public async Task DispatchCompletedAsync_ShouldSkipSettings_WhenAlreadyClaimed()
    {
        var settings = Substitute.For<IServerSettingsService>();
        settings.GetAsync(ServerSettingKeys.Scrobbling, Arg.Any<CancellationToken>())
            .Returns("""{"Enabled":false}""");
        var dispatcher = CreateDispatcher(settings, new ScrobbleCompletionGate());
        var sessionId = Guid.NewGuid();

        await dispatcher.DispatchCompletedAsync(
            sessionId, Guid.NewGuid(), "u", Guid.NewGuid(), 100, 100, null, CancellationToken.None);
        await dispatcher.DispatchCompletedAsync(
            sessionId, Guid.NewGuid(), "u", Guid.NewGuid(), 100, 100, null, CancellationToken.None);

        await settings.Received(1).GetAsync(ServerSettingKeys.Scrobbling, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DispatchProgressAsync_ShouldNotTouchSettings_WhenNotPlaying()
    {
        var settings = Substitute.For<IServerSettingsService>();
        var dispatcher = CreateDispatcher(settings, new ScrobbleCompletionGate());

        await dispatcher.DispatchProgressAsync(
            Guid.NewGuid(), Guid.NewGuid(), "u", Guid.NewGuid(),
            PlaybackState.Paused, 10, 100, null, CancellationToken.None);

        await settings.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    [Test]
    public async Task DispatchAsync_ShouldNotEnqueue_WhenScrobblingDisabled()
    {
        var queue = Substitute.For<IScrobbleQueue>();
        var settings = Substitute.For<IServerSettingsService>();
        settings.GetAsync(ServerSettingKeys.Scrobbling, Arg.Any<CancellationToken>())
            .Returns("""{"Enabled":false}""");
        var dispatcher = CreateDispatcher(settings, new ScrobbleCompletionGate(), queue);
        var domainEvent = new PlaybackStateChangedEvent(
            PlaybackState.Playing,
            PlaybackState.Paused,
            Guid.NewGuid(),
            "u",
            Guid.NewGuid(),
            "Title",
            "Movie",
            Guid.NewGuid(),
            10,
            100,
            null,
            null,
            null);

        await dispatcher.DispatchAsync(domainEvent, CancellationToken.None);

        queue.DidNotReceive().Enqueue(Arg.Any<ScrobbleWorkItem>());
    }

    private static ScrobbleDispatcher CreateDispatcher(
        IServerSettingsService settings,
        IScrobbleCompletionGate gate,
        IScrobbleQueue? queue = null)
    {
        var context = Substitute.For<IApplicationDbContext>();
        var sessions = new List<MediaPlaybackSession>().BuildMockDbSet();
        context.MediaPlaybackSessions.Returns(sessions);

        var throttle = Substitute.For<IScrobbleProgressThrottle>();
        throttle.TryAcquire(Arg.Any<Guid>(), Arg.Any<TimeSpan>()).Returns(true);

        return new ScrobbleDispatcher(
            context,
            Substitute.For<IIdentityService>(),
            settings,
            Substitute.For<IScrobbleConfigProtector>(),
            queue ?? Substitute.For<IScrobbleQueue>(),
            new ScrobblePayloadFactory(context),
            Substitute.For<ISharedProfilePlaybackResolver>(),
            throttle,
            gate,
            NullLogger<ScrobbleDispatcher>.Instance);
    }
}
