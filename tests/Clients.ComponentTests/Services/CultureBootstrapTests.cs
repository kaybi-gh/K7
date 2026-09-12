using System.Globalization;
using K7.Clients.Shared.Services;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class CultureBootstrapTests
{
    private IJSRuntime _js = null!;
    private IServerInfoService _serverInfo = null!;
    private RecordingNavigationManager _navigation = null!;
    private CultureInfo _previousCulture = null!;
    private CultureInfo _previousUiCulture = null!;
    private CultureInfo? _previousDefaultCulture;
    private CultureInfo? _previousDefaultUiCulture;

    [SetUp]
    public void SetUp()
    {
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        _previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        _previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        _js = Substitute.For<IJSRuntime>();
        _serverInfo = Substitute.For<IServerInfoService>();
        _navigation = new RecordingNavigationManager();
    }

    [TearDown]
    public void TearDown()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _previousDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _previousDefaultUiCulture;
    }

    [Test]
    public async Task InitializeAsync_ShouldNotReload_WhenServerInfoIsMissing()
    {
        SetUiCulture("fr");
        StubSaved(null);
        _serverInfo.GetServerInfoAsync(Arg.Any<CancellationToken>()).Returns((ServerInfoDto?)null);

        await CultureBootstrap.InitializeAsync(_js, _serverInfo, _navigation);

        _navigation.Navigated.Should().BeFalse();
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Should().Be("fr");
    }

    [Test]
    public async Task InitializeAsync_ShouldReload_WhenServerDefaultDiffersFromCurrent()
    {
        SetUiCulture("en");
        StubSaved(null);
        _serverInfo.GetServerInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServerInfoDto { DefaultLanguage = "fr" });

        await CultureBootstrap.InitializeAsync(_js, _serverInfo, _navigation);

        _navigation.Navigated.Should().BeTrue();
        _navigation.ForceLoad.Should().BeTrue();
        CultureInfo.DefaultThreadCurrentUICulture!.TwoLetterISOLanguageName.Should().Be("fr");
    }

    [Test]
    public async Task InitializeAsync_ShouldNotReload_WhenCurrentAlreadyMatchesServerDefault()
    {
        SetUiCulture("fr");
        StubSaved(null);
        _serverInfo.GetServerInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServerInfoDto { DefaultLanguage = "fr" });

        await CultureBootstrap.InitializeAsync(_js, _serverInfo, _navigation);

        _navigation.Navigated.Should().BeFalse();
    }

    [Test]
    public async Task InitializeAsync_ShouldPreferSavedCultureOverServerDefault()
    {
        SetUiCulture("fr");
        StubSaved("en");
        _serverInfo.GetServerInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServerInfoDto { DefaultLanguage = "fr" });

        await CultureBootstrap.InitializeAsync(_js, _serverInfo, _navigation);

        _navigation.Navigated.Should().BeTrue();
        CultureInfo.DefaultThreadCurrentUICulture!.TwoLetterISOLanguageName.Should().Be("en");
        await _serverInfo.DidNotReceive().GetServerInfoAsync(Arg.Any<CancellationToken>());
    }

    private void StubSaved(string? saved)
    {
        _js.InvokeAsync<string?>(
                "blazorCulture.getSaved",
                Arg.Any<CancellationToken>(),
                Arg.Any<object?[]>())
            .Returns(new ValueTask<string?>(saved));
    }

    private static void SetUiCulture(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public bool Navigated { get; private set; }
        public bool ForceLoad { get; private set; }

        public RecordingNavigationManager() => Initialize("https://app/", "https://app/");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Navigated = true;
            ForceLoad = options.ForceLoad;
        }
    }
}
