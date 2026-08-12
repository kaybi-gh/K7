using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AudioPlaybackProgressTrackerTests
{
    private IAudioPlayerService _audio = null!;
    private IStreamingService _streaming = null!;
    private IDeviceStorageService _storage = null!;
    private IConnectivityService _connectivity = null!;
    private IPlaybackJournal _journal = null!;
    private AudioPlaybackProgressTracker _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _audio = Substitute.For<IAudioPlayerService>();
        _streaming = Substitute.For<IStreamingService>();
        _storage = Substitute.For<IDeviceStorageService>();
        _connectivity = Substitute.For<IConnectivityService>();
        _journal = Substitute.For<IPlaybackJournal>();

        _connectivity.IsOnline.Returns(true);
        _storage.Get(PreferenceKeys.DEVICE_ID).Returns(Guid.NewGuid().ToString());
        _audio.Duration.Returns(330.773);
        _audio.CurrentTime.Returns(299.861);

        _sut = new AudioPlaybackProgressTracker(
            _audio,
            _streaming,
            _storage,
            _connectivity,
            _journal);
        _sut.SetCanReport(true);
    }

    [TearDown]
    public void TearDown() => _sut.Dispose();

    [Test]
    public async Task Report_ShouldUseStreamSessionId_WhenSourceProvidesIt()
    {
        var mediaId = Guid.NewGuid();
        var indexedFileId = Guid.NewGuid();
        var streamSessionId = Guid.NewGuid();

        _audio.CurrentTrackChanged += Raise.Event<Action<AudioQueueItem?>>(new AudioQueueItem
        {
            MediaId = mediaId,
            IndexedFileId = indexedFileId,
            Title = "Track",
            Artist = "Artist",
            AlbumTitle = "Album"
        });

        _audio.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource
        {
            Url = "https://example.test/a.mp3",
            StreamSessionId = streamSessionId
        });

        _audio.PlaybackStateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);
        await Task.Delay(50);

        await _streaming.Received().ReportPlaybackProgressAsync(
            mediaId,
            streamSessionId,
            Arg.Any<Guid>(),
            299.861,
            330.773,
            (int)PlaybackState.Playing,
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>());
    }

    [Test]
    public async Task Report_ShouldNotSend_WhenNoCurrentTrack()
    {
        _audio.PlaybackStateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Playing);
        await Task.Delay(50);

        await _streaming.DidNotReceiveWithAnyArgs()
            .ReportPlaybackProgressAsync(default, default, default, default, default, default, default);
    }
}
