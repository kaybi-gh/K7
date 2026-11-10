using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AutoplayServiceTests
{
    private static readonly MusicRadioRequest _activeRadio = new() { RadioType = "Discovery", Title = "Discovery" };

    private IAudioPlayerService _audio = null!;
    private IMusicRadioPlaybackService _radio = null!;
    private IDeviceStorageService _storage = null!;
    private IServerPreferencesService _serverPreferences = null!;
    private AutoplayService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _audio = Substitute.For<IAudioPlayerService>();
        _radio = Substitute.For<IMusicRadioPlaybackService>();
        _storage = Substitute.For<IDeviceStorageService>();
        _serverPreferences = Substitute.For<IServerPreferencesService>();

        _storage.Get(PreferenceKeys.AUTOPLAY_ENABLED, true).Returns(true);
        _serverPreferences.GetMusicIntelligenceStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new MusicIntelligenceStatusDto { IsAvailable = true });
        _audio.CurrentTrack.Returns(Track());

        _sut = new AutoplayService(_audio, _radio, _storage, _serverPreferences);
    }

    [TearDown]
    public void TearDown() => _sut.Dispose();

    [Test]
    public void PlaybackEnded_ShouldExtendTheActiveRadioAndPlayOn_WhenARadioIsPlaying()
    {
        GivenAnActiveRadio();
        _radio.ExtendAsync(Arg.Any<CancellationToken>()).Returns(true);

        End();

        _radio.Received(1).ExtendAsync(Arg.Any<CancellationToken>());
        _audio.Received(1).NextAsync(Arg.Any<CancellationToken>());
        _radio.DidNotReceive().StartAsync(Arg.Any<MusicRadioRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void PlaybackEnded_ShouldNotSwitchToASimilarRadio_WhenTheActiveRadioHasNothingLeft()
    {
        GivenAnActiveRadio();
        _radio.ExtendAsync(Arg.Any<CancellationToken>()).Returns(false);

        End();

        _audio.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
        _radio.DidNotReceive().StartAsync(Arg.Any<MusicRadioRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void PlaybackEnded_ShouldNotSwitchToASimilarRadio_WhenExtendingTheActiveRadioFails()
    {
        GivenAnActiveRadio();
        _radio.ExtendAsync(Arg.Any<CancellationToken>()).Returns<Task<bool>>(_ => throw new HttpRequestException());

        End();

        _radio.DidNotReceive().StartAsync(Arg.Any<MusicRadioRequest>(), Arg.Any<CancellationToken>());

        // The failure does not leave the service stuck: the next end of queue tries again.
        _radio.ExtendAsync(Arg.Any<CancellationToken>()).Returns(true);
        End();
        _audio.Received(1).NextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void PlaybackEnded_ShouldExtendTheActiveRadio_WhenAutoplayIsOff()
    {
        _sut.SetEnabled(false);
        GivenAnActiveRadio();
        _radio.ExtendAsync(Arg.Any<CancellationToken>()).Returns(true);

        End();

        _audio.Received(1).NextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void PlaybackEnded_ShouldStartASimilarRadioFromTheLastTrack_WhenNoRadioIsPlaying()
    {
        var track = Track();
        _audio.CurrentTrack.Returns(track);

        End();

        _radio.DidNotReceive().ExtendAsync(Arg.Any<CancellationToken>());
        _radio.Received(1).StartAsync(
            Arg.Is<MusicRadioRequest>(r => r.RadioType == "Sonic" && r.SeedTrackId == track.MediaId),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void PlaybackEnded_ShouldStartASimilarRadio_WhenTheRadioRequestOutlivedItsQueue()
    {
        // A request left over while the player no longer plays a radio (queue replaced by an album).
        _radio.ActiveRequest.Returns(_activeRadio);
        _audio.ActiveRadioTitle.Returns((string?)null);

        End();

        _radio.DidNotReceive().ExtendAsync(Arg.Any<CancellationToken>());
        _radio.Received(1).StartAsync(Arg.Is<MusicRadioRequest>(r => r.RadioType == "Sonic"), Arg.Any<CancellationToken>());
    }

    [Test]
    public void PlaybackEnded_ShouldDoNothing_WhenAutoplayIsOffAndNoRadioIsPlaying()
    {
        _sut.SetEnabled(false);

        End();

        _radio.DidNotReceive().StartAsync(Arg.Any<MusicRadioRequest>(), Arg.Any<CancellationToken>());
        _radio.DidNotReceive().ExtendAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void OtherPlaybackStates_ShouldBeIgnored()
    {
        GivenAnActiveRadio();

        _audio.PlaybackStateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Paused);

        _radio.DidNotReceive().ExtendAsync(Arg.Any<CancellationToken>());
        _radio.DidNotReceive().StartAsync(Arg.Any<MusicRadioRequest>(), Arg.Any<CancellationToken>());
    }

    private void GivenAnActiveRadio()
    {
        _radio.ActiveRequest.Returns(_activeRadio);
        _audio.ActiveRadioTitle.Returns(_activeRadio.Title);
    }

    private void End() => _audio.PlaybackStateChanged += Raise.Event<Action<PlaybackState>>(PlaybackState.Ended);

    private static AudioQueueItem Track() => new()
    {
        IndexedFileId = Guid.NewGuid(),
        MediaId = Guid.NewGuid(),
        Title = "Track",
        Artist = null,
        AlbumTitle = null
    };
}
