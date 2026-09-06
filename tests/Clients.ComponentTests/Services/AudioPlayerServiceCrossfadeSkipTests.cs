using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Shared;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AudioPlayerServiceCrossfadeSkipTests
{
    private IStreamUriService _streamUri = null!;
    private IDeviceStorageService _storage = null!;
    private AudioPlayerService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _streamUri = Substitute.For<IStreamUriService>();
        _storage = Substitute.For<IDeviceStorageService>();
        _storage.Get(Arg.Any<PreferenceKey<bool>>(), Arg.Any<bool>())
            .Returns(ci => ci.ArgAt<bool>(1));
        _storage.Get(Arg.Any<PreferenceKey<double>>(), Arg.Any<double>())
            .Returns(ci => ci.ArgAt<double>(1));
        _storage.Get(Arg.Any<PreferenceKey<string?>>(), Arg.Any<string?>())
            .Returns(ci => ci.ArgAt<string?>(1));

        _sut = new AudioPlayerService(_streamUri, _storage);
        _sut.ToggleAdaptiveCrossfade();
        _sut.CrossfadeRequested += (_, _) => Task.CompletedTask;
    }

    [Test]
    public async Task PreviousAsync_ShouldReturnToOutgoingTrack_WhenNotificationShowsIncoming()
    {
        var tracks = CreateTracks(3);
        await _sut.PlayTracksAsync(tracks, 0);
        _sut.CurrentTime = 170;
        await _sut.OnCrossfadeNeededAsync();

        _sut.CurrentIndex.Should().Be(1);
        var seeked = false;
        _sut.SeekRequested += _ =>
        {
            seeked = true;
            return Task.CompletedTask;
        };

        await _sut.PreviousAsync();

        seeked.Should().BeFalse();
        _sut.CurrentIndex.Should().Be(0);
        _sut.CurrentTrack!.MediaId.Should().Be(tracks[0].MediaId);
    }

    [Test]
    public async Task NextAsync_ShouldSkipIncomingTrack_WhenNotificationShowsIncoming()
    {
        var tracks = CreateTracks(3);
        await _sut.PlayTracksAsync(tracks, 0);
        _sut.CurrentTime = 170;
        await _sut.OnCrossfadeNeededAsync();

        _sut.CurrentIndex.Should().Be(1);

        await _sut.NextAsync();

        _sut.CurrentIndex.Should().Be(2);
        _sut.CurrentTrack!.MediaId.Should().Be(tracks[2].MediaId);
    }

    [Test]
    public async Task OnCrossfadeNeededAsync_ShouldKeepDisplayedTrackOnOutgoing_WhenPlayingAdvances()
    {
        var tracks = CreateTracks(2);
        await _sut.PlayTracksAsync(tracks, 0);
        _sut.CurrentTime = 170;

        await _sut.OnCrossfadeNeededAsync();

        _sut.CurrentPlayingTrack!.MediaId.Should().Be(tracks[1].MediaId);
        _sut.CurrentDisplayedTrack!.MediaId.Should().Be(tracks[0].MediaId);
        _sut.CurrentTrack!.MediaId.Should().Be(tracks[1].MediaId);
    }

    [Test]
    public async Task NotifyCrossfadeCompleted_ShouldAlignDisplayedTrackWithPlaying()
    {
        var tracks = CreateTracks(2);
        await _sut.PlayTracksAsync(tracks, 0);
        _sut.CurrentTime = 170;
        await _sut.OnCrossfadeNeededAsync();

        _sut.NotifyCrossfadeCompleted();

        _sut.CurrentPlayingTrack!.MediaId.Should().Be(tracks[1].MediaId);
        _sut.CurrentDisplayedTrack!.MediaId.Should().Be(tracks[1].MediaId);
    }

    [Test]
    public async Task PreviousAsync_ShouldRestart_WhenNotInCrossfadeHandoffAndTimeElapsed()
    {
        var tracks = CreateTracks(2);
        await _sut.PlayTracksAsync(tracks, 0);
        _sut.CurrentTime = 40;
        var seekTo = -1d;
        _sut.SeekRequested += t =>
        {
            seekTo = t;
            return Task.CompletedTask;
        };

        await _sut.PreviousAsync();

        seekTo.Should().Be(0);
        _sut.CurrentIndex.Should().Be(0);
    }

    private static List<AudioQueueItem> CreateTracks(int count)
    {
        var tracks = new List<AudioQueueItem>(count);
        for (var i = 0; i < count; i++)
        {
            tracks.Add(new AudioQueueItem
            {
                IndexedFileId = Guid.NewGuid(),
                MediaId = Guid.NewGuid(),
                Title = $"Track {i + 1}",
                Artist = "Artist",
                AlbumTitle = $"Album {i}",
                Duration = 180,
                LocalPath = $"file:///track-{i}.mp3"
            });
        }

        return tracks;
    }
}
