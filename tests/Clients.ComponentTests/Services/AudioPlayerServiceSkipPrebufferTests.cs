using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Shared;
using K7.Shared.Dtos;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AudioPlayerServiceSkipPrebufferTests
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

        _streamUri.GetOrCreateSessionAsync(
                Arg.Any<Guid>(),
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => CreateSession(ci.ArgAt<Guid>(0)));

        _sut = new AudioPlayerService(_streamUri, _storage);
    }

    [Test]
    public async Task NextAsync_ShouldReusePrebufferedSource_WhenSkippingToPreparedTrack()
    {
        var tracks = CreateStreamingTracks(2);
        await _sut.PlayTracksAsync(tracks, 0);

        PlayerSource? prebuffered = null;
        _sut.GaplessPrebufferRequested += source =>
        {
            prebuffered = source;
            return Task.CompletedTask;
        };
        await _sut.OnGaplessPrebufferNeededAsync();
        prebuffered.Should().NotBeNull();

        PlayerSource? skipped = null;
        _sut.SourceChanged += source => skipped = source;

        await _sut.NextAsync();

        skipped.Should().NotBeNull();
        skipped!.Url.Should().Be(prebuffered!.Url);
        skipped.IndexedFileId.Should().Be(tracks[1].IndexedFileId);
        await _streamUri.Received(2).GetOrCreateSessionAsync(
            Arg.Any<Guid>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NextAsync_ShouldDropStaleLoad_WhenANewerSkipSupersedesIt()
    {
        var tracks = CreateStreamingTracks(3);
        var firstFile = tracks[0].IndexedFileId;
        var tcs = new TaskCompletionSource<StreamingSessionDto>();
        _streamUri.GetOrCreateSessionAsync(
                firstFile,
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => CreateSession(firstFile));
        _streamUri.GetOrCreateSessionAsync(
                tracks[1].IndexedFileId,
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => tcs.Task);
        _streamUri.GetOrCreateSessionAsync(
                tracks[2].IndexedFileId,
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => CreateSession(tracks[2].IndexedFileId));

        await _sut.PlayTracksAsync(tracks, 0);

        var sources = new List<string?>();
        _sut.SourceChanged += source => sources.Add(source.Url);

        var firstSkip = _sut.NextAsync();
        await _sut.NextAsync();
        tcs.SetResult(CreateSession(tracks[1].IndexedFileId));
        await firstSkip;

        sources.Should().ContainSingle(url => url != null && url.Contains(tracks[2].IndexedFileId.ToString()));
        sources.Should().NotContain(url => url != null && url.Contains(tracks[1].IndexedFileId.ToString()));
        _sut.CurrentIndex.Should().Be(2);
    }

    private static StreamingSessionDto CreateSession(Guid indexedFileId)
    {
        var sessionId = Guid.NewGuid();
        return new StreamingSessionDto
        {
            Id = sessionId,
            IndexedFileId = indexedFileId,
            PlaybackSettings = new PlaybackSettingsDto(),
            Source = new IndexedFileStreamUri
            {
                Uri = new Uri($"https://k7.example/stream/{indexedFileId}/{sessionId}"),
                MimeType = "audio/mpeg"
            }
        };
    }

    private static List<AudioQueueItem> CreateStreamingTracks(int count)
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
                Duration = 180
            });
        }

        return tracks;
    }
}
