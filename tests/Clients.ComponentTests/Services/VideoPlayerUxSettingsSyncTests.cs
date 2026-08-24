using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Web.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class VideoPlayerUxSettingsSyncTests
{
    private FakeVideoPlayerSettingsHub _hub = null!;
    private IStreamUriService _streamUri = null!;
    private IDeviceStorageService _storage = null!;
    private PlayerService _player = null!;
    private IDeviceService _device = null!;
    private IJSRuntime _js = null!;
    private ServiceProvider _serviceProvider = null!;
    private VideoPlayerUxSettingsSync _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _hub = new FakeVideoPlayerSettingsHub();
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

        _player = new PlayerService(_streamUri, _storage);
        _device = Substitute.For<IDeviceService>();
        _device.CachedDeviceType.Returns(DeviceType.Desktop);
        _js = Substitute.For<IJSRuntime>();
        _js.InvokeAsync<IJSVoidResult>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?[]>())
            .Returns(new ValueTask<IJSVoidResult>(Substitute.For<IJSVoidResult>()));

        var services = new ServiceCollection();
        services.AddSingleton<IVideoPlayerSettingsHubEvents>(_hub);
        services.AddSingleton<IPlayerService>(_player);
        services.AddSingleton(_device);
        services.AddSingleton(_js);
        services.AddSingleton<IVideoPlayerUxSettingsSync, VideoPlayerUxSettingsSync>();
        _serviceProvider = services.BuildServiceProvider();
        _sut = (VideoPlayerUxSettingsSync)_serviceProvider.GetRequiredService<IVideoPlayerUxSettingsSync>();
    }

    [TearDown]
    public void TearDown()
    {
        _sut.Dispose();
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task HubUpdate_ShouldApplyUxSettingsAndSubtitleStyle()
    {
        var settings = new VideoPlayerSettingsDto
        {
            SkipBackSeconds = 25,
            SkipForwardSeconds = 35,
            SubtitleFontSize = SubtitleFontSize.Large
        };

        _hub.Raise(settings);
        await Task.Delay(50);

        _player.SkipBackSeconds.Should().Be(25);
        _player.SkipForwardSeconds.Should().Be(35);
        _player.VideoPlayerUxSettings.Should().BeSameAs(settings);
        await _js.Received().InvokeAsync<IJSVoidResult>(
            "applySubtitleStyle",
            Arg.Any<CancellationToken>(),
            Arg.Is<object?[]>(args => args.Length == 1));
    }

    [Test]
    public async Task HubUpdate_ShouldNotAlterActivePlaybackState()
    {
        var mediaId = Guid.NewGuid();
        _player.Source = new() { Url = "https://example/stream.m3u8", MediaId = mediaId };
        _player.PlaybackState = PlaybackState.Playing;
        _player.CurrentTime = 842;
        _player.Duration = 7200;
        _player.Volume = 0.6;
        _player.PlaybackRate = 1.25;

        var playRequested = false;
        var pauseRequested = false;
        var seekRequested = false;
        var stopRequested = false;
        _player.PlayRequested += () => { playRequested = true; return Task.CompletedTask; };
        _player.PauseRequested += () => { pauseRequested = true; return Task.CompletedTask; };
        _player.SeekRequested += _ => { seekRequested = true; return Task.CompletedTask; };
        _player.StopRequested += () => { stopRequested = true; return Task.CompletedTask; };

        _hub.Raise(new VideoPlayerSettingsDto { SkipBackSeconds = 12, SubtitleFontSize = SubtitleFontSize.Small });
        await Task.Delay(50);

        _player.PlaybackState.Should().Be(PlaybackState.Playing);
        _player.CurrentTime.Should().Be(842);
        _player.Duration.Should().Be(7200);
        _player.Volume.Should().Be(0.6);
        _player.PlaybackRate.Should().Be(1.25);
        _player.Source.MediaId.Should().Be(mediaId);
        _player.Source.Url.Should().Be("https://example/stream.m3u8");
        playRequested.Should().BeFalse();
        pauseRequested.Should().BeFalse();
        seekRequested.Should().BeFalse();
        stopRequested.Should().BeFalse();
    }

    [Test]
    public async Task HubUpdate_ShouldNotThrow_WhenJsIsDisconnected()
    {
        _js.InvokeAsync<IJSVoidResult>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?[]>())
            .Returns(_ => ValueTask.FromException<IJSVoidResult>(new JSDisconnectedException("disconnected")));

        var act = async () =>
        {
            _hub.Raise(new VideoPlayerSettingsDto());
            await Task.Delay(50);
        };

        await act.Should().NotThrowAsync();
        _player.VideoPlayerUxSettings.Should().NotBeNull();
    }

    [Test]
    public async Task HubUpdate_ShouldStopApplying_AfterDispose()
    {
        _sut.Dispose();

        _hub.Raise(new VideoPlayerSettingsDto { SkipBackSeconds = 99 });
        await Task.Delay(50);

        _player.SkipBackSeconds.Should().Be(10);
        await _js.DidNotReceiveWithAnyArgs().InvokeAsync<IJSVoidResult>(default!, default);
    }

    private sealed class FakeVideoPlayerSettingsHub : IVideoPlayerSettingsHubEvents
    {
        public event Action<VideoPlayerSettingsDto>? VideoPlayerSettingsUpdated;

        public void Raise(VideoPlayerSettingsDto settings) =>
            VideoPlayerSettingsUpdated?.Invoke(settings);
    }
}
