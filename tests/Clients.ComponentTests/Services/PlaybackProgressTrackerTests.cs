using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class PlaybackProgressTrackerTests
{
    private IPlayerService _player = null!;
    private IStreamingService _streaming = null!;
    private IDeviceStorageService _storage = null!;
    private IConnectivityService _connectivity = null!;
    private IPlaybackJournal _journal = null!;
    private ILocalUserService _localUsers = null!;
    private MediaCacheStore _cache = null!;
    private PlaybackProgressTracker _sut = null!;
    private PlayerSource _source = null!;

    [SetUp]
    public void SetUp()
    {
        _player = Substitute.For<IPlayerService>();
        _streaming = Substitute.For<IStreamingService>();
        _storage = Substitute.For<IDeviceStorageService>();
        _connectivity = Substitute.For<IConnectivityService>();
        _journal = Substitute.For<IPlaybackJournal>();
        _localUsers = Substitute.For<ILocalUserService>();
        _cache = new MediaCacheStore();

        _source = new PlayerSource
        {
            Url = "https://example.test/manifest.m3u8",
            StreamSessionId = Guid.NewGuid(),
            PendingSeekTime = 2800
        };

        _player.Source.Returns(_source);
        _player.Duration.Returns(7200d);
        _player.CurrentTime.Returns(0d);
        _connectivity.IsOnline.Returns(true);
        _storage.Get(PreferenceKeys.DEVICE_ID).Returns(Guid.NewGuid().ToString());
        _localUsers.GetLastActive().Returns(new LocalUser
        {
            IdentityUserId = "user-a",
            UserName = "A",
            RefreshToken = "rt"
        });

        _sut = new PlaybackProgressTracker(
            _player,
            _streaming,
            _storage,
            _connectivity,
            _journal,
            _localUsers,
            _cache);
    }

    [TearDown]
    public void TearDown() => _sut.Dispose();

    [Test]
    public async Task Report_ShouldSkipNearZero_WhenPendingSeekIsActive()
    {
        var mediaId = Guid.NewGuid();
        _sut.StartTracking(mediaId, isAuthenticated: true);

        _player.CurrentTime.Returns(12d);
        _player.PlaybackStateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);

        await Task.Delay(50);

        await _streaming.DidNotReceiveWithAnyArgs()
            .ReportPlaybackProgressAsync(default, default, default, default, default, default, default);
    }

    [Test]
    public async Task Report_ShouldSend_WhenPositionReachesPendingSeek()
    {
        var mediaId = Guid.NewGuid();
        _sut.StartTracking(mediaId, isAuthenticated: true);

        _source.PendingSeekTime = null;
        _player.CurrentTime.Returns(2805d);
        _player.PlaybackStateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);

        await Task.Delay(50);

        await _streaming.Received().ReportPlaybackProgressAsync(
            mediaId,
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            2805d,
            7200d,
            (int)PlaybackState.Playing,
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>());
    }

    [Test]
    public async Task Report_ShouldSkipSpuriousZero_AfterSignificantProgress()
    {
        var mediaId = Guid.NewGuid();
        _source.PendingSeekTime = null;
        _sut.StartTracking(mediaId, isAuthenticated: true);

        _player.CurrentTime.Returns(120d);
        _player.PlaybackStateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);
        await Task.Delay(50);

        _player.CurrentTime.Returns(2d);
        _player.PlaybackStateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);
        await Task.Delay(50);

        await _streaming.Received(1).ReportPlaybackProgressAsync(
            mediaId,
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            120d,
            7200d,
            (int)PlaybackState.Playing,
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>());

        await _streaming.DidNotReceive().ReportPlaybackProgressAsync(
            mediaId,
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            2d,
            7200d,
            Arg.Any<int>(),
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>());
    }

    [Test]
    public async Task Report_ShouldJournalForLastActiveUser_WhenOffline()
    {
        var mediaId = Guid.NewGuid();
        var indexedFileId = Guid.NewGuid();
        _source.PendingSeekTime = null;
        _connectivity.IsOnline.Returns(false);
        _sut.StartTracking(mediaId, isAuthenticated: true, indexedFileId: indexedFileId);

        _player.CurrentTime.Returns(120d);
        _player.PlaybackStateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);
        await Task.Delay(50);

        await _streaming.DidNotReceiveWithAnyArgs()
            .ReportPlaybackProgressAsync(default, default, default, default, default, default, default);

        await _journal.Received().RecordProgressAsync(
            mediaId,
            indexedFileId,
            120d,
            7200d,
            "user-a",
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Report_ShouldNotJournal_WhenOfflineAndNoLastActiveUser()
    {
        var mediaId = Guid.NewGuid();
        _source.PendingSeekTime = null;
        _connectivity.IsOnline.Returns(false);
        _localUsers.GetLastActive().Returns((LocalUser?)null);
        _sut.StartTracking(mediaId, isAuthenticated: true, indexedFileId: Guid.NewGuid());

        _player.CurrentTime.Returns(120d);
        _player.PlaybackStateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);
        await Task.Delay(50);

        await _journal.DidNotReceiveWithAnyArgs()
            .RecordProgressAsync(default, default, default, default, default!);
    }
}
