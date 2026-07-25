using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AudioPlayerServiceShuffleTests
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
    }

    [Test]
    public async Task OnTrackEnded_ShouldAdvance_WhenSameAlbumShuffleDeclinesCrossfade()
    {
        var tracks = CreateAlbumTracks(count: 3);
        await _sut.PlayShuffledAsync(tracks);

        var firstId = _sut.CurrentTrack!.MediaId;
        _sut.PlaybackState = PlaybackState.Playing;

        // Same-album adaptive crossfade returns 0. Repeated arm attempts used to
        // drain the shuffle order via GetNextIndex before the track actually ended.
        await _sut.OnCrossfadeNeededAsync();
        await _sut.OnCrossfadeNeededAsync();
        await _sut.OnCrossfadeNeededAsync();
        await _sut.OnCrossfadeNeededAsync();

        await _sut.OnTrackEndedAsync();

        _sut.PlaybackState.Should().NotBe(PlaybackState.Ended);
        _sut.CurrentTrack.Should().NotBeNull();
        _sut.CurrentTrack!.MediaId.Should().NotBe(firstId);
    }

    [Test]
    public async Task OnTrackEnded_ShouldAdvance_WhenTwoTrackAlbumShuffleDeclinesCrossfade()
    {
        var tracks = CreateAlbumTracks(count: 2);
        await _sut.PlayShuffledAsync(tracks);

        var firstId = _sut.CurrentTrack!.MediaId;
        _sut.PlaybackState = PlaybackState.Playing;

        await _sut.OnCrossfadeNeededAsync();
        await _sut.OnCrossfadeNeededAsync();
        await _sut.OnTrackEndedAsync();

        _sut.PlaybackState.Should().NotBe(PlaybackState.Ended);
        _sut.CurrentTrack!.MediaId.Should().NotBe(firstId);
    }

    private static List<AudioQueueItem> CreateAlbumTracks(int count)
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
                AlbumTitle = "Same Album",
                Duration = 180,
                LocalPath = $"file:///track-{i}.mp3"
            });
        }

        return tracks;
    }
}
