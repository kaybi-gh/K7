using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class TrailerDialogHelperTests
{
    private IDeviceService _device = null!;
    private IExternalLinkService _externalLinks = null!;
    private IK7DialogService _dialogs = null!;
    private IAmbientThemeService _ambientTheme = null!;
    private IUserPreferencesService _preferences = null!;
    private IK7Snackbar _snackbar = null!;
    private IStringLocalizer _localizer = null!;

    [SetUp]
    public void SetUp()
    {
        _device = Substitute.For<IDeviceService>();
        _externalLinks = Substitute.For<IExternalLinkService>();
        _dialogs = Substitute.For<IK7DialogService>();
        _ambientTheme = Substitute.For<IAmbientThemeService>();
        _preferences = Substitute.For<IUserPreferencesService>();
        _snackbar = Substitute.For<IK7Snackbar>();
        _localizer = Substitute.For<IStringLocalizer>();

        _device.CachedDeviceType.Returns(DeviceType.Desktop);
        _device.GetClientType().Returns(ClientType.Native);
        _preferences.GetEffectiveVideoPlayerSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new VideoPlayerSettingsDto());
        _localizer["TrailerOpenFailed"].Returns(new LocalizedString("TrailerOpenFailed", "open failed"));
        _dialogs.ShowAsync<TrailerDialog>(Arg.Any<string>(), Arg.Any<K7DialogParameters>(), Arg.Any<K7DialogOptions>())
            .Returns(Substitute.For<IK7DialogReference>());
    }

    [Test]
    public async Task OpenAsync_ShouldDoNothing_WhenNoTrailers()
    {
        await TrailerDialogHelper.OpenAsync(
            [],
            Guid.NewGuid(),
            "Trailer",
            _device,
            _externalLinks,
            _dialogs,
            _ambientTheme,
            _preferences,
            _snackbar,
            _localizer);

        await _ambientTheme.DidNotReceiveWithAnyArgs().InterruptAsync(default, default);
        await _externalLinks.DidNotReceiveWithAnyArgs().OpenAsync(default!);
        await _dialogs.DidNotReceiveWithAnyArgs().ShowAsync<TrailerDialog>(default!, default, default);
    }

    [Test]
    public async Task OpenAsync_ShouldShowOverlay_WhenNativeAndSettingOff()
    {
        var mediaId = Guid.NewGuid();

        await OpenAsync(CreateYouTube(), mediaId);

        await _ambientTheme.Received(1).InterruptAsync(mediaId, Arg.Any<CancellationToken>());
        await _externalLinks.DidNotReceiveWithAnyArgs().OpenAsync(default!);
        await _dialogs.Received(1).ShowAsync<TrailerDialog>(
            Arg.Any<string>(),
            Arg.Any<K7DialogParameters>(),
            Arg.Is<K7DialogOptions>(o => o.BackdropClass == TrailerDialogHelper.VideoOverlayBackdropClass));
    }

    [Test]
    public async Task OpenAsync_ShouldOpenExternally_WhenNativeAndSettingOn()
    {
        _preferences.GetEffectiveVideoPlayerSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new VideoPlayerSettingsDto { OpenTrailersExternally = true });
        _externalLinks.OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await OpenAsync(CreateYouTube());

        await _externalLinks.Received(1).OpenAsync(
            "https://www.youtube.com/watch?v=abc",
            Arg.Any<CancellationToken>());
        await _dialogs.DidNotReceiveWithAnyArgs().ShowAsync<TrailerDialog>(default!, default, default);
        _snackbar.DidNotReceiveWithAnyArgs().Add(default!, default);
    }

    [Test]
    public async Task OpenAsync_ShouldShowOverlay_WhenTvAndSettingOff()
    {
        _device.GetClientType().Returns(ClientType.Web);
        _device.CachedDeviceType.Returns(DeviceType.TV);

        await OpenAsync(CreateYouTube());

        await _externalLinks.DidNotReceiveWithAnyArgs().OpenAsync(default!);
        await _dialogs.Received(1).ShowAsync<TrailerDialog>(
            Arg.Any<string>(),
            Arg.Any<K7DialogParameters>(),
            Arg.Any<K7DialogOptions>());
    }

    [Test]
    public async Task OpenAsync_ShouldOpenExternally_WhenTvAndSettingOn()
    {
        _device.GetClientType().Returns(ClientType.Web);
        _device.CachedDeviceType.Returns(DeviceType.TV);
        _preferences.GetEffectiveVideoPlayerSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new VideoPlayerSettingsDto { OpenTrailersExternally = true });
        _externalLinks.OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await OpenAsync(CreateYouTube());

        await _externalLinks.Received(1).OpenAsync(
            "https://www.youtube.com/watch?v=abc",
            Arg.Any<CancellationToken>());
        await _dialogs.DidNotReceiveWithAnyArgs().ShowAsync<TrailerDialog>(default!, default, default);
    }

    [Test]
    public async Task OpenAsync_ShouldShowOverlay_WhenPreferencesFail()
    {
        _preferences.GetEffectiveVideoPlayerSettingsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<VideoPlayerSettingsDto>>(_ => throw new HttpRequestException());

        await OpenAsync(CreateYouTube());

        await _externalLinks.DidNotReceiveWithAnyArgs().OpenAsync(default!);
        await _dialogs.Received(1).ShowAsync<TrailerDialog>(
            Arg.Any<string>(),
            Arg.Any<K7DialogParameters>(),
            Arg.Any<K7DialogOptions>());
    }

    [Test]
    public async Task OpenAsync_ShouldShowSnackbar_WhenNativeExternalOpenFails()
    {
        _preferences.GetEffectiveVideoPlayerSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new VideoPlayerSettingsDto { OpenTrailersExternally = true });
        _externalLinks.OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await OpenAsync(CreateYouTube());

        _snackbar.Received(1).Add("open failed", K7Severity.Error);
        await _dialogs.DidNotReceiveWithAnyArgs().ShowAsync<TrailerDialog>(default!, default, default);
    }

    [Test]
    public async Task OpenAsync_ShouldFallBackToOverlay_WhenWebExternalOpenFails()
    {
        _device.GetClientType().Returns(ClientType.Web);
        _device.CachedDeviceType.Returns(DeviceType.TV);
        _preferences.GetEffectiveVideoPlayerSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new VideoPlayerSettingsDto { OpenTrailersExternally = true });
        _externalLinks.OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await OpenAsync(CreateYouTube());

        _snackbar.DidNotReceiveWithAnyArgs().Add(default!, default);
        await _dialogs.Received(1).ShowAsync<TrailerDialog>(
            Arg.Any<string>(),
            Arg.Any<K7DialogParameters>(),
            Arg.Any<K7DialogOptions>());
    }

    [Test]
    public async Task OpenAsync_ShouldOpenExternally_WhenSiteIsNotEmbeddable()
    {
        _externalLinks.OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var trailer = new TrailerDto
        {
            Key = "https://example.com/trailer",
            Name = "Clip",
            Site = "External",
            Type = "Trailer"
        };

        await OpenAsync(trailer);

        await _externalLinks.Received(1).OpenAsync(
            "https://example.com/trailer",
            Arg.Any<CancellationToken>());
        await _dialogs.DidNotReceiveWithAnyArgs().ShowAsync<TrailerDialog>(default!, default, default);
    }

    private Task OpenAsync(TrailerDto trailer, Guid? mediaId = null) =>
        TrailerDialogHelper.OpenAsync(
            [trailer],
            mediaId ?? Guid.NewGuid(),
            "Trailer",
            _device,
            _externalLinks,
            _dialogs,
            _ambientTheme,
            _preferences,
            _snackbar,
            _localizer);

    private static TrailerDto CreateYouTube() => new()
    {
        Key = "abc",
        Name = "Trailer",
        Site = "YouTube",
        Type = "Trailer"
    };
}
