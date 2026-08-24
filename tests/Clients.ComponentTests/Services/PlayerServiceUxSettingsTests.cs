using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Web.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class PlayerServiceUxSettingsTests
{
    private IStreamUriService _streamUri = null!;
    private IDeviceStorageService _storage = null!;
    private PlayerService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _streamUri = Substitute.For<IStreamUriService>();
        _storage = Substitute.For<IDeviceStorageService>();
        _storage.Get(Arg.Any<PreferenceKey<int>>(), Arg.Any<int>())
            .Returns(ci => ci.ArgAt<int>(1));
        _storage.Get(Arg.Any<PreferenceKey<double>>(), Arg.Any<double>())
            .Returns(ci => ci.ArgAt<double>(1));
        _storage.Get(Arg.Any<PreferenceKey<bool>>(), Arg.Any<bool>())
            .Returns(ci => ci.ArgAt<bool>(1));
        _storage.Get(Arg.Any<PreferenceKey<string?>>(), Arg.Any<string?>())
            .Returns(ci => ci.ArgAt<string?>(1));

        _sut = new PlayerService(_streamUri, _storage);
    }

    [Test]
    public void ApplyVideoPlayerUxSettings_ShouldUpdateSkipSecondsAndCache()
    {
        var settings = new VideoPlayerSettingsDto
        {
            SkipBackSeconds = 15,
            SkipForwardSeconds = 45,
            SubtitleFontSize = SubtitleFontSize.Large
        };

        _sut.ApplyVideoPlayerUxSettings(settings);

        _sut.SkipBackSeconds.Should().Be(15);
        _sut.SkipForwardSeconds.Should().Be(45);
        _sut.VideoPlayerUxSettings.Should().BeSameAs(settings);
        _storage.Received(1).Set(PreferenceKeys.VIDEO_SKIP_BACK_SECONDS, 15);
        _storage.Received(1).Set(PreferenceKeys.VIDEO_SKIP_FORWARD_SECONDS, 45);
    }

    [Test]
    public void ApplyVideoPlayerUxSettings_ShouldClampSkipSecondsToAtLeastOne()
    {
        var settings = new VideoPlayerSettingsDto
        {
            SkipBackSeconds = 0,
            SkipForwardSeconds = -3
        };

        _sut.ApplyVideoPlayerUxSettings(settings);

        _sut.SkipBackSeconds.Should().Be(1);
        _sut.SkipForwardSeconds.Should().Be(1);
    }

    [Test]
    public void ApplyVideoPlayerUxSettings_ShouldRaisePlayerUxSettingsChangedOnce()
    {
        var callCount = 0;
        _sut.PlayerUxSettingsChanged += () => callCount++;

        _sut.ApplyVideoPlayerUxSettings(new VideoPlayerSettingsDto { SkipBackSeconds = 5, SkipForwardSeconds = 20 });

        callCount.Should().Be(1);
    }

    [Test]
    public void ApplyVideoPlayerUxSettings_ShouldPropagateException_WhenSubscriberThrows()
    {
        _sut.PlayerUxSettingsChanged += () => throw new InvalidOperationException("subscriber fault");

        var act = () => _sut.ApplyVideoPlayerUxSettings(new VideoPlayerSettingsDto());

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ApplyVideoPlayerUxSettings_ShouldNotTouchPlaybackTransportState()
    {
        var mediaId = Guid.NewGuid();
        _sut.Source = new PlayerSource { Url = "https://example/video.m3u8", MediaId = mediaId };
        _sut.PlaybackState = PlaybackState.Playing;
        _sut.CurrentTime = 300;
        _sut.Duration = 5400;
        _sut.Volume = 0.75;
        _sut.PlaybackRate = 1.5;
        _sut.IsMuted = false;

        _sut.ApplyVideoPlayerUxSettings(new VideoPlayerSettingsDto
        {
            SkipBackSeconds = 20,
            SkipForwardSeconds = 40,
            SubtitleFontSize = SubtitleFontSize.Large
        });

        _sut.PlaybackState.Should().Be(PlaybackState.Playing);
        _sut.CurrentTime.Should().Be(300);
        _sut.Duration.Should().Be(5400);
        _sut.Volume.Should().Be(0.75);
        _sut.PlaybackRate.Should().Be(1.5);
        _sut.IsMuted.Should().BeFalse();
        _sut.Source.MediaId.Should().Be(mediaId);
        _sut.Source.Url.Should().Be("https://example/video.m3u8");
    }
}
