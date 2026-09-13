using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class MediaPlayerServiceTests
{
    private IPlayerService _video = null!;
    private IAudioPlayerService _audio = null!;
    private MediaPlayerService _sut = null!;
    private bool _videoVisible;
    private bool _audioVisible;

    [SetUp]
    public void SetUp()
    {
        _video = Substitute.For<IPlayerService>();
        _audio = Substitute.For<IAudioPlayerService>();
        _videoVisible = false;
        _audioVisible = false;
        _video.IsVisible.Returns(_ => _videoVisible);
        _audio.IsVisible.Returns(_ => _audioVisible);
        _video.HideAsync().Returns(_ =>
        {
            _videoVisible = false;
            return Task.CompletedTask;
        });
        _audio.HideAsync().Returns(_ =>
        {
            _audioVisible = false;
            return Task.CompletedTask;
        });
        _sut = new MediaPlayerService(_video, _audio);
    }

    [TearDown]
    public void TearDown() => _sut.Dispose();

    [Test]
    public void AudioSourceChanged_ShouldHideVideo_WhenVideoIsVisible()
    {
        _videoVisible = true;

        _audio.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "track" });

        _sut.ActivePlayer.Should().Be(ActivePlayerType.Audio);
        _video.Received(1).Stop();
        _video.Received(1).HideAsync();
    }

    [Test]
    public void AudioBecameVisible_ShouldHideVideo_BeforeSourceChanges()
    {
        _videoVisible = true;
        _audioVisible = true;

        _audio.IsVisibleChanged += Raise.Event<Action>();

        _sut.ActivePlayer.Should().Be(ActivePlayerType.Audio);
        _video.Received(1).Stop();
        _video.Received(1).HideAsync();
    }

    [Test]
    public void NextAudioSource_ShouldHideVideo_WhenAudioAlreadyActiveAndVideoStillVisible()
    {
        _audio.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "one" });
        _video.DidNotReceive().Stop();

        _videoVisible = true;
        _audio.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "two" });

        _sut.ActivePlayer.Should().Be(ActivePlayerType.Audio);
        _video.Received(1).Stop();
        _video.Received(1).HideAsync();
    }

    [Test]
    public void AudioSourceChanged_ShouldNotTouchVideo_WhenVideoAlreadyHidden()
    {
        _audio.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "track" });

        _video.DidNotReceive().Stop();
        _video.DidNotReceive().HideAsync();
    }

    [Test]
    public void AudioBecameHidden_ShouldNotSwitchActivePlayer()
    {
        _audioVisible = true;
        _audio.IsVisibleChanged += Raise.Event<Action>();
        _sut.ActivePlayer.Should().Be(ActivePlayerType.Audio);

        _audioVisible = false;
        _audio.IsVisibleChanged += Raise.Event<Action>();

        _sut.ActivePlayer.Should().Be(ActivePlayerType.Audio);
        _video.DidNotReceive().Stop();
    }

    [Test]
    public void VideoSourceChanged_ShouldHideAudio_WhenAudioIsVisible()
    {
        _audioVisible = true;

        _video.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "movie" });

        _sut.ActivePlayer.Should().Be(ActivePlayerType.Video);
        _audio.Received(1).Stop();
        _audio.Received(1).HideAsync();
    }

    [Test]
    public void VideoBecameVisible_ShouldHideAudio_WhenAudioIsVisible()
    {
        _audioVisible = true;
        _videoVisible = true;

        _video.IsVisibleChanged += Raise.Event<Action>();

        _sut.ActivePlayer.Should().Be(ActivePlayerType.Video);
        _audio.Received(1).Stop();
        _audio.Received(1).HideAsync();
    }

    [Test]
    public void NextVideoSource_ShouldHideAudio_WhenVideoAlreadyActiveAndAudioStillVisible()
    {
        _video.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "one" });
        _audio.DidNotReceive().Stop();

        _audioVisible = true;
        _video.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "two" });

        _sut.ActivePlayer.Should().Be(ActivePlayerType.Video);
        _audio.Received(1).Stop();
        _audio.Received(1).HideAsync();
    }

    [Test]
    public void VideoBecameHidden_ShouldNotSwitchActivePlayer()
    {
        _videoVisible = true;
        _video.IsVisibleChanged += Raise.Event<Action>();
        _sut.ActivePlayer.Should().Be(ActivePlayerType.Video);

        _videoVisible = false;
        _video.IsVisibleChanged += Raise.Event<Action>();

        _sut.ActivePlayer.Should().Be(ActivePlayerType.Video);
        _audio.DidNotReceive().Stop();
    }

    [Test]
    public void ActivePlayerChanged_ShouldFireOnce_WhenSwitchingFromVideoToAudio()
    {
        var seen = new List<ActivePlayerType>();
        _sut.ActivePlayerChanged += seen.Add;

        _videoVisible = true;
        _video.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "movie" });
        _audioVisible = true;
        _audio.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "track" });
        _audio.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "next" });

        seen.Should().Equal(ActivePlayerType.Video, ActivePlayerType.Audio);
    }

    [Test]
    public void Dispose_ShouldStopHidingVideo_WhenAudioStarts()
    {
        _sut.Dispose();
        _videoVisible = true;
        _audioVisible = true;

        _audio.IsVisibleChanged += Raise.Event<Action>();
        _audio.SourceChanged += Raise.Event<Action<PlayerSource>>(new PlayerSource { Url = "track" });

        _video.DidNotReceive().Stop();
        _video.DidNotReceive().HideAsync();
    }
}
