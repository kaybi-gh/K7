using System.Security.Claims;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class PlaybackSyncServiceTests
{
    private IPlaybackJournal _journal = null!;
    private IStreamingService _streaming = null!;
    private IRatingService _ratings = null!;
    private IConnectivityService _connectivity = null!;
    private StubAuthStateProvider _auth = null!;
    private PlaybackSyncService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        AppReadySignal.Reset();

        _journal = Substitute.For<IPlaybackJournal>();
        _streaming = Substitute.For<IStreamingService>();
        _ratings = Substitute.For<IRatingService>();
        _connectivity = Substitute.For<IConnectivityService>();
        _auth = new StubAuthStateProvider(Anonymous());

        _connectivity.IsOnline.Returns(true);
        _journal.GetPendingEventsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _sut = new PlaybackSyncService(
            _journal,
            _streaming,
            _ratings,
            _connectivity,
            _auth,
            NullLogger<PlaybackSyncService>.Instance);
    }

    [TearDown]
    public void TearDown() => AppReadySignal.Reset();

    [Test]
    public async Task Sync_ShouldNotQueryJournal_WhenAppIsNotReady()
    {
        _auth.SetUser(OnlineUser("user-a"));

        await _sut.SyncPendingEventsAsync();

        await _journal.DidNotReceiveWithAnyArgs().GetPendingEventsAsync(default!);
    }

    [Test]
    public async Task Sync_ShouldNotQueryJournal_WhenAnonymous()
    {
        AppReadySignal.Signal();

        await _sut.SyncPendingEventsAsync();

        await _journal.DidNotReceiveWithAnyArgs().GetPendingEventsAsync(default!);
    }

    [Test]
    public async Task Sync_ShouldNotQueryJournal_WhenOfflineSession()
    {
        AppReadySignal.Signal();
        _auth.SetUser(OfflineUser("user-a"));

        await _sut.SyncPendingEventsAsync();

        await _journal.DidNotReceiveWithAnyArgs().GetPendingEventsAsync(default!);
    }

    [Test]
    public async Task Sync_ShouldNotQueryJournal_WhenOffline()
    {
        AppReadySignal.Signal();
        _auth.SetUser(OnlineUser("user-a"));
        _connectivity.IsOnline.Returns(false);

        await _sut.SyncPendingEventsAsync();

        await _journal.DidNotReceiveWithAnyArgs().GetPendingEventsAsync(default!);
    }

    [Test]
    public async Task Sync_ShouldSendOnlyCurrentUserEvents_WhenOnlineAfterReady()
    {
        AppReadySignal.Signal();
        _auth.SetUser(OnlineUser("user-a"));

        var progressId = Guid.NewGuid();
        var ratingId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        _journal.GetPendingEventsAsync("user-a", Arg.Any<CancellationToken>()).Returns(
        [
            new PendingPlaybackEvent
            {
                Id = progressId,
                MediaId = mediaId,
                IndexedFileId = fileId,
                EventType = PlaybackEventType.Progress,
                Position = 42,
                Duration = 100,
                Timestamp = DateTimeOffset.UtcNow,
                IdentityUserId = "user-a"
            },
            new PendingPlaybackEvent
            {
                Id = ratingId,
                MediaId = mediaId,
                IndexedFileId = Guid.Empty,
                EventType = PlaybackEventType.Rated,
                Position = 0,
                Duration = 0,
                Timestamp = DateTimeOffset.UtcNow,
                IdentityUserId = "user-a",
                RatingValue = 8
            },
            new PendingPlaybackEvent
            {
                Id = Guid.NewGuid(),
                MediaId = mediaId,
                IndexedFileId = fileId,
                EventType = PlaybackEventType.Progress,
                Position = 10,
                Duration = 100,
                Timestamp = DateTimeOffset.UtcNow,
                IdentityUserId = "user-b"
            }
        ]);

        await _sut.SyncPendingEventsAsync();

        await _streaming.Received(1).ReportPlaybackProgressAsync(
            mediaId,
            Arg.Any<Guid>(),
            fileId,
            42,
            100,
            3,
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
        await _ratings.Received(1).RateMediaAsync(mediaId, 8, Arg.Any<CancellationToken>());
        await _journal.Received(1).MarkSyncedAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { progressId, ratingId })),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Sync_ShouldRequestCurrentUserEvents_WhenConnectivityReturns()
    {
        AppReadySignal.Signal();
        _auth.SetUser(OnlineUser("user-a"));

        _connectivity.ConnectivityChanged += Raise.Event<Action<bool>>(true);
        await Task.Delay(50);

        await _journal.Received().GetPendingEventsAsync("user-a", Arg.Any<CancellationToken>());
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal OnlineUser(string id) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "Bearer"));

    private static ClaimsPrincipal OfflineUser(string id) =>
        new(new ClaimsIdentity([new Claim("sub", id)], AuthIdentity.OfflineAuthenticationType));

    private sealed class StubAuthStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState _state;

        public StubAuthStateProvider(ClaimsPrincipal user) =>
            _state = new AuthenticationState(user);

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(_state);

        public void SetUser(ClaimsPrincipal user) =>
            _state = new AuthenticationState(user);
    }
}
