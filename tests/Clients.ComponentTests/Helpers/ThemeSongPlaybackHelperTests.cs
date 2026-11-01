using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class ThemeSongPlaybackHelperTests
{
    private static readonly Guid MediaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string ThemeUrl = "https://server/api/medias/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/theme";

    private IMediaService _mediaService = null!;
    private IUserPreferencesService _preferences = null!;
    private IAmbientThemeService _ambientTheme = null!;

    [SetUp]
    public void SetUp()
    {
        _mediaService = Substitute.For<IMediaService>();
        _mediaService.GetMediaThemeSongUrl(MediaId).Returns(ThemeUrl);

        _preferences = Substitute.For<IUserPreferencesService>();
        _preferences.GetEffectiveVideoPlayerSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new VideoPlayerSettingsDto { PlayThemeSongs = true });

        _ambientTheme = Substitute.For<IAmbientThemeService>();
    }

    [Test]
    public async Task TryStartAsync_ShouldNotDownloadBytes_WhenTheThemeStreams()
    {
        _ambientTheme.TryKeepOrStartStreamingAsync(MediaId, ThemeUrl, Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await StartAsync();

        await _ambientTheme.Received(1).TryKeepOrStartStreamingAsync(MediaId, ThemeUrl, Arg.Any<double>(), Arg.Any<CancellationToken>());
        await _ambientTheme.DidNotReceiveWithAnyArgs().KeepOrStartAsync(default, default!, default!, default, default);
    }

    [Test]
    public async Task TryStartAsync_ShouldNotStream_WhenThemeSongsAreDisabled()
    {
        _preferences.GetEffectiveVideoPlayerSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new VideoPlayerSettingsDto { PlayThemeSongs = false });

        await StartAsync();

        await _ambientTheme.DidNotReceiveWithAnyArgs().TryKeepOrStartStreamingAsync(default, default!, default, default);
    }

    private Task StartAsync() =>
        ThemeSongPlaybackHelper.TryStartAsync(
            MediaId,
            hasThemeSong: true,
            _mediaService,
            _preferences,
            _ambientTheme,
            Substitute.For<IAudioPlayerService>(),
            Substitute.For<IPlayerService>(),
            Substitute.For<IDeviceStorageService>());
}
