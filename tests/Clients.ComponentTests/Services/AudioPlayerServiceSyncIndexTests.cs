using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AudioPlayerServiceSyncIndexTests
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
    public async Task SyncCurrentIndexFromExternalPlayer_ShouldChangeTrack_WithoutSourceChanged()
    {
        var tracks = CreateTracks(3);
        await _sut.PlayTracksAsync(tracks, 0);

        var sourceChanges = 0;
        _sut.SourceChanged += _ => sourceChanges++;
        var trackChanges = 0;
        _sut.CurrentTrackChanged += _ => trackChanges++;

        _sut.SyncCurrentIndexFromExternalPlayer(1);

        _sut.CurrentIndex.Should().Be(1);
        _sut.CurrentTrack!.MediaId.Should().Be(tracks[1].MediaId);
        sourceChanges.Should().Be(0);
        trackChanges.Should().Be(1);
    }

    [Test]
    public async Task SyncCurrentIndexFromExternalPlayer_ShouldNoOp_WhenIndexUnchanged()
    {
        var tracks = CreateTracks(2);
        await _sut.PlayTracksAsync(tracks, 0);

        var trackChanges = 0;
        _sut.CurrentTrackChanged += _ => trackChanges++;

        _sut.SyncCurrentIndexFromExternalPlayer(0);

        _sut.CurrentIndex.Should().Be(0);
        trackChanges.Should().Be(0);
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
                AlbumTitle = "Album",
                Duration = 180,
                LocalPath = $"file:///track-{i}.mp3"
            });
        }

        return tracks;
    }
}
