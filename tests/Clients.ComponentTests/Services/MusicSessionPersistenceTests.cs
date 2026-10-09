using AwesomeAssertions;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.Enums;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class MusicSessionPersistenceTests
{
    [Test]
    public async Task BindAndRestore_ShouldSkip_WhenRememberIsOff()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        store.Read(Arg.Any<string>()).Returns(StoredSnapshot());
        var sut = CreateSut(audio, store, remember: false);

        await sut.BindAndRestoreAsync("user-1");

        audio.Queue.Should().BeEmpty();
        audio.IsVisible.Should().BeFalse();
    }

    [Test]
    public async Task BindAndRestore_ShouldWriteLocal_WhenTheQueueChangesAfterAnEmptyBind()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        var sut = CreateSut(audio, store, remember: true, online: false);
        await sut.BindAndRestoreAsync("user-1");

        await audio.PlayTracksAsync([Track("FromCar")]);

        store.Received().Write(
            Arg.Any<string>(),
            Arg.Is<MusicSessionSnapshotDto>(snapshot =>
                snapshot.Items.Any(item => item.Title == "FromCar")));
    }

    [Test]
    public async Task BindAndRestore_ShouldSkip_WhenQueueIsAlreadyLoaded()
    {
        var audio = CreateAudio();
        await audio.PlayTracksAsync(
        [
            new AudioQueueItem
            {
                IndexedFileId = Guid.NewGuid(),
                MediaId = Guid.NewGuid(),
                Title = "Live",
                Artist = "Artist",
                AlbumTitle = "Album"
            }
        ]);
        var store = Substitute.For<IMusicSessionStore>();
        store.Read(Arg.Any<string>()).Returns(StoredSnapshot());
        var sut = CreateSut(audio, store, remember: true);

        await sut.BindAndRestoreAsync("user-1");

        audio.Queue.Should().ContainSingle();
        audio.CurrentTrack!.Title.Should().Be("Live");
    }

    [Test]
    public async Task BindAndRestore_ShouldDropLocal_WhenSyncedSessionIsGoneFromServer()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        store.Read(Arg.Any<string>()).Returns(StoredSnapshot());
        store.IsSynced(Arg.Any<string>()).Returns(true);
        var deviceId = Guid.NewGuid();
        var api = Substitute.For<IMusicSessionApi>();
        api.GetMusicSessionAsync(deviceId, Arg.Any<CancellationToken>())
            .Returns((MusicSessionSnapshotDto?)null);
        var sut = CreateSut(audio, store, remember: true, api: api, online: true, deviceId: deviceId);

        await sut.BindAndRestoreAsync("user-1");

        audio.IsVisible.Should().BeFalse();
        audio.Queue.Should().BeEmpty();
        store.Received().Delete(Arg.Any<string>());
    }

    [Test]
    public async Task BindAndRestore_ShouldUploadLocal_WhenItNeverReachedTheServer()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        store.Read(Arg.Any<string>()).Returns(StoredSnapshot());
        store.IsSynced(Arg.Any<string>()).Returns(false);
        var deviceId = Guid.NewGuid();
        var api = Substitute.For<IMusicSessionApi>();
        api.GetMusicSessionAsync(deviceId, Arg.Any<CancellationToken>())
            .Returns((MusicSessionSnapshotDto?)null);
        var sut = CreateSut(audio, store, remember: true, api: api, online: true, deviceId: deviceId);

        await sut.BindAndRestoreAsync("user-1");

        audio.CurrentTrack!.Title.Should().Be("Saved");
        audio.IsVisible.Should().BeTrue();
        store.DidNotReceive().Delete(Arg.Any<string>());
        await api.Received().UpsertMusicSessionAsync(
            Arg.Is<UpsertMusicSessionRequest>(request => request.DeviceId == deviceId),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task BindAndRestore_ShouldUseServer_WhenOnline()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        store.Read(Arg.Any<string>()).Returns(StoredSnapshot());
        var deviceId = Guid.NewGuid();
        var remote = StoredSnapshot("Remote");
        var api = Substitute.For<IMusicSessionApi>();
        api.GetMusicSessionAsync(deviceId, Arg.Any<CancellationToken>()).Returns(remote);
        var sut = CreateSut(audio, store, remember: true, api: api, online: true, deviceId: deviceId);

        await sut.BindAndRestoreAsync("user-1");

        audio.CurrentTrack!.Title.Should().Be("Remote");
        audio.IsAwaitingRestoredPlay.Should().BeTrue();
        store.DidNotReceive().Delete(Arg.Any<string>());
    }

    [Test]
    public async Task BindAndRestore_ShouldUseLocal_WhenOffline()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        store.Read(Arg.Any<string>()).Returns(StoredSnapshot());
        var sut = CreateSut(audio, store, remember: true, online: false);

        await sut.BindAndRestoreAsync("user-1");

        audio.CurrentTrack!.Title.Should().Be("Saved");
        audio.IsVisible.Should().BeTrue();
    }

    [Test]
    public async Task FinishedQueue_ShouldDropTheSavedSession_AndKeepThePlayer()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        var deviceId = Guid.NewGuid();
        var api = Substitute.For<IMusicSessionApi>();
        var sut = CreateSut(audio, store, remember: true, api: api, online: true, deviceId: deviceId);
        await sut.BindAndRestoreAsync("user-1");
        await audio.PlayTracksAsync(
        [
            new AudioQueueItem
            {
                IndexedFileId = Guid.NewGuid(),
                MediaId = Guid.NewGuid(),
                Title = "Last",
                Artist = "Artist",
                AlbumTitle = "Album"
            }
        ]);

        await audio.NextAsync();

        audio.IsQueueExhausted.Should().BeTrue();
        audio.Queue.Should().ContainSingle();
        audio.IsVisible.Should().BeTrue();
        store.Received().Delete(Arg.Any<string>());
        await api.Received().DeleteMusicSessionAsync(deviceId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RepeatAll_ShouldKeepTheSavedSession_WhenTheQueueWraps()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        var deviceId = Guid.NewGuid();
        var api = Substitute.For<IMusicSessionApi>();
        var sut = CreateSut(audio, store, remember: true, api: api, online: true, deviceId: deviceId);
        await sut.BindAndRestoreAsync("user-1");
        await audio.PlayTracksAsync(
        [
            Track("One"),
            Track("Two")
        ], startIndex: 1);
        audio.SetRepeat(RepeatMode.All);

        await audio.NextAsync();

        audio.IsQueueExhausted.Should().BeFalse();
        audio.CurrentTrack!.Title.Should().Be("One");
        store.DidNotReceive().Delete(Arg.Any<string>());
    }

    [Test]
    public async Task FinishedQueue_ShouldSaveAgain_WhenPlaybackRestarts()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        var deviceId = Guid.NewGuid();
        var api = Substitute.For<IMusicSessionApi>();
        var sut = CreateSut(audio, store, remember: true, api: api, online: true, deviceId: deviceId);
        await sut.BindAndRestoreAsync("user-1");
        await audio.PlayTracksAsync([Track("Last")]);
        await audio.NextAsync();
        api.ClearReceivedCalls();

        audio.Play();
        audio.PlaybackState = PlaybackState.Playing;
        await Task.Delay(700);

        audio.IsQueueExhausted.Should().BeFalse();
        audio.IsVisible.Should().BeTrue();
        await api.Received().UpsertMusicSessionAsync(
            Arg.Is<UpsertMusicSessionRequest>(request => request.DeviceId == deviceId),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EndedPlayback_ShouldKeepTheSavedSession_WhenTheQueueIsNotExhausted()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        var deviceId = Guid.NewGuid();
        var api = Substitute.For<IMusicSessionApi>();
        var sut = CreateSut(audio, store, remember: true, api: api, online: true, deviceId: deviceId);
        await sut.BindAndRestoreAsync("user-1");
        await audio.PlayTracksAsync([Track("Now")]);

        audio.PlaybackState = PlaybackState.Ended;

        audio.IsQueueExhausted.Should().BeFalse();
        store.DidNotReceive().Delete(Arg.Any<string>());
        await api.DidNotReceive().DeleteMusicSessionAsync(deviceId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task BindAndRestore_ShouldKeepLocal_WhenTheServerCannotBeReached()
    {
        var audio = CreateAudio();
        var store = Substitute.For<IMusicSessionStore>();
        store.Read(Arg.Any<string>()).Returns(StoredSnapshot());
        var deviceId = Guid.NewGuid();
        var api = Substitute.For<IMusicSessionApi>();
        api.GetMusicSessionAsync(deviceId, Arg.Any<CancellationToken>())
            .Returns<Task<MusicSessionSnapshotDto?>>(_ => throw new HttpRequestException("offline"));
        var sut = CreateSut(audio, store, remember: true, api: api, online: true, deviceId: deviceId);

        await sut.BindAndRestoreAsync("user-1");

        audio.CurrentTrack!.Title.Should().Be("Saved");
        store.DidNotReceive().Delete(Arg.Any<string>());
    }

    private static AudioPlayerService CreateAudio()
    {
        var streamUri = Substitute.For<IStreamUriService>();
        var storage = Substitute.For<IDeviceStorageService>();
        storage.Get(Arg.Any<PreferenceKey<bool>>(), Arg.Any<bool>())
            .Returns(ci => ci.ArgAt<bool>(1));
        storage.Get(Arg.Any<PreferenceKey<double>>(), Arg.Any<double>())
            .Returns(ci => ci.ArgAt<double>(1));
        storage.Get(Arg.Any<PreferenceKey<string?>>(), Arg.Any<string?>())
            .Returns(ci => ci.ArgAt<string?>(1));
        return new AudioPlayerService(streamUri, storage);
    }

    private static AudioQueueItem Track(string title) =>
        new()
        {
            IndexedFileId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            Title = title,
            Artist = "Artist",
            AlbumTitle = "Album"
        };

    private static MusicSessionPersistenceService CreateSut(
        AudioPlayerService audio,
        IMusicSessionStore store,
        bool remember,
        IMusicSessionApi? api = null,
        bool online = false,
        Guid? deviceId = null)
    {
        var preferences = Substitute.For<IUserPreferencesService>();
        preferences.GetEffectiveAudioPlayerSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new AudioPlayerSettingsDto { RememberMusicSession = remember });

        var syncPlay = Substitute.For<ISyncPlayService>();
        syncPlay.IsInGroup.Returns(false);

        var profiles = Substitute.For<ISharedProfileSessionService>();
        profiles.ActiveGroupId.Returns((Guid?)null);

        var connectivity = Substitute.For<IConnectivityService>();
        connectivity.IsOnline.Returns(online);

        var storage = Substitute.For<IDeviceStorageService>();
        if (deviceId is Guid id)
            storage.Get(PreferenceKeys.DEVICE_ID).Returns(id.ToString());

        return new MusicSessionPersistenceService(
            audio,
            Substitute.For<IMusicRadioPlaybackService>(),
            store,
            api ?? Substitute.For<IMusicSessionApi>(),
            storage,
            preferences,
            Substitute.For<IMediaService>(),
            Substitute.For<IPlaylistService>(),
            Substitute.For<IK7ServerService>(),
            syncPlay,
            profiles,
            connectivity,
            NullLogger<MusicSessionPersistenceService>.Instance);
    }

    private static MusicSessionSnapshotDto StoredSnapshot(string title = "Saved") =>
        new()
        {
            CurrentMediaId = Guid.NewGuid(),
            Items =
            [
                new MusicSessionTrackDto
                {
                    IndexedFileId = Guid.NewGuid(),
                    MediaId = Guid.NewGuid(),
                    Title = title
                }
            ]
        };
}
