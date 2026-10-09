using AwesomeAssertions;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using NSubstitute;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AudioPlayerServiceRestoreTests
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
    public void RestorePaused_ShouldShowPausedQueue_WithoutOpeningAStream()
    {
        var opened = 0;
        _sut.SourceChanged += _ => opened++;
        var currentId = Guid.NewGuid();

        _sut.RestorePaused(Snapshot(currentId, positionSeconds: 42));

        opened.Should().Be(0);
        _ = _streamUri.DidNotReceiveWithAnyArgs().GetOrCreateSessionAsync(default);
        _sut.PlaybackState.Should().Be(PlaybackState.Paused);
        _sut.IsVisible.Should().BeTrue();
        _sut.CurrentTrack!.MediaId.Should().Be(currentId);
        _sut.CurrentTime.Should().Be(42);
        _sut.Queue.Should().HaveCount(2);
    }

    [Test]
    public async Task ResolveRestoredSource_ShouldExposeTheResumePosition_UntilPrepareCompletes()
    {
        _streamUri.GetOrCreateSessionAsync(
                Arg.Any<Guid>(),
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(new StreamingSessionDto
            {
                Id = Guid.NewGuid(),
                IndexedFileId = Guid.NewGuid(),
                PlaybackSettings = new PlaybackSettingsDto(),
                Source = new IndexedFileStreamUri
                {
                    Uri = new Uri("https://k7.example/stream/restored"),
                    MimeType = "audio/mpeg"
                }
            });
        var opened = 0;
        _sut.SourceChanged += _ => opened++;
        var currentId = Guid.NewGuid();
        _sut.RestorePaused(Snapshot(currentId, positionSeconds: 42));

        var source = await _sut.ResolveRestoredSourceAsync();

        opened.Should().Be(0);
        _sut.IsAwaitingRestoredPlay.Should().BeTrue();
        source.Should().NotBeNull();
        source!.PendingSeekTime.Should().Be(42);
        source.MediaId.Should().Be(currentId);
        source.TryConsumePendingSeek(out var seconds).Should().BeTrue();
        seconds.Should().Be(42);

        _sut.CompleteRestoredPrepare();
        _sut.IsAwaitingRestoredPlay.Should().BeFalse();
        (await _sut.ResolveRestoredSourceAsync()).Should().BeNull();
    }

    [Test]
    public async Task RestorePaused_ShouldKeepTheExistingQueue_WhenOneIsAlreadyLoaded()
    {
        var existingId = Guid.NewGuid();
        await _sut.PlayTracksAsync(
        [
            new AudioQueueItem
            {
                IndexedFileId = Guid.NewGuid(),
                MediaId = existingId,
                Title = "Already playing",
                Artist = "Artist",
                AlbumTitle = "Album"
            }
        ]);

        _sut.RestorePaused(Snapshot(Guid.NewGuid(), positionSeconds: 10));

        _sut.CurrentTrack!.MediaId.Should().Be(existingId);
        _sut.Queue.Should().ContainSingle();
    }

    [Test]
    public async Task HoldResumePosition_ShouldSeekBack_WhenPlayFollowsAStopAtZero()
    {
        var mediaId = Guid.NewGuid();
        await _sut.PlayTracksAsync(
        [
            new AudioQueueItem
            {
                IndexedFileId = Guid.NewGuid(),
                MediaId = mediaId,
                Title = "Can't Sleep",
                Artist = "K.Flay",
                AlbumTitle = "Life as a Dog",
                LocalPath = "track.mp3",
                Duration = 200
            }
        ]);

        _sut.CurrentTime = 95;
        _sut.HoldResumePosition();
        _sut.Stop();
        _sut.CurrentTime = 0;

        _sut.CurrentTime.Should().Be(95);

        PlayerSource? resumed = null;
        _sut.SourceChanged += source => resumed = source;
        _sut.Play();
        await Task.Yield();

        resumed.Should().NotBeNull();
        resumed!.PendingSeekTime.Should().Be(95);
        resumed.MediaId.Should().Be(mediaId);
    }

    private static MusicSessionSnapshotDto Snapshot(Guid currentId, double positionSeconds) =>
        new()
        {
            SourceKind = MusicSessionSourceKind.AdHoc,
            CurrentMediaId = currentId,
            CurrentIndex = 1,
            PositionSeconds = positionSeconds,
            Items =
            [
                Track(Guid.NewGuid(), "Previous"),
                Track(currentId, "Current")
            ]
        };

    private static MusicSessionTrackDto Track(Guid mediaId, string title) =>
        new()
        {
            IndexedFileId = Guid.NewGuid(),
            MediaId = mediaId,
            Title = title
        };
}
