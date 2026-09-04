using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AmbientThemeServiceTests
{
    private static readonly Guid SerieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherMediaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PersonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private IJSRuntime _js = null!;
    private NavigationManager _navigation = null!;
    private AmbientThemeService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _js = Substitute.For<IJSRuntime>();
        _js.InvokeAsync<IJSVoidResult>(Arg.Any<string>(), Arg.Any<object?[]>())
            .Returns(new ValueTask<IJSVoidResult>(Substitute.For<IJSVoidResult>()));

        _navigation = new StubNavigationManager();
        _sut = new AmbientThemeService(_js, _navigation, leaveGrace: TimeSpan.FromMilliseconds(40));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _sut.DisposeAsync();
    }

    [Test]
    public async Task KeepOrStartAsync_ShouldPlay_WhenNewMedia()
    {
        await StartThemeAsync(SerieId, "https://server/api/medias/a/theme");

        _sut.CurrentMediaId.Should().Be(SerieId);
        _sut.IsFinished.Should().BeFalse();
        await AssertPlayBytesCalledAsync(times: 1);
    }

    [Test]
    public async Task KeepOrStartAsync_ShouldNotReplay_WhenSameMediaStillActive()
    {
        var url = "https://server/api/medias/a/theme";
        await StartThemeAsync(SerieId, url);
        await _sut.KeepOrStartAsync(SerieId, url, [9, 9, 9]);

        await AssertPlayBytesCalledAsync(times: 1);
        _sut.CurrentMediaId.Should().Be(SerieId);
    }

    [Test]
    public async Task KeepOrStartAsync_ShouldNotReplay_WhenSameMediaFinished()
    {
        var url = "https://server/api/medias/a/theme";
        await StartThemeAsync(SerieId, url);
        _sut.NotifyNaturalEnded();

        _sut.IsFinished.Should().BeTrue();
        _sut.CurrentMediaId.Should().Be(SerieId);

        await _sut.KeepOrStartAsync(SerieId, url, [9, 9, 9]);
        await _sut.KeepOrStartAsync(SerieId, url, []);

        await AssertPlayBytesCalledAsync(times: 1);
        _sut.IsFinished.Should().BeTrue();
    }

    [Test]
    public async Task KeepOrStartAsync_ShouldPlayOtherMedia_AfterFinished()
    {
        await StartThemeAsync(SerieId, "https://server/api/medias/a/theme");
        _sut.NotifyNaturalEnded();

        await StartThemeAsync(OtherMediaId, "https://server/api/medias/b/theme");

        _sut.CurrentMediaId.Should().Be(OtherMediaId);
        _sut.IsFinished.Should().BeFalse();
        await AssertPlayBytesCalledAsync(times: 2);
    }

    [Test]
    public async Task Navigation_ShouldKeepTheme_WhenGoingToPerson()
    {
        await StartThemeAsync(SerieId, "https://server/api/medias/a/theme");

        _sut.HandleLocationChanged($"https://app/persons/{PersonId}");
        await Task.Delay(80);

        _sut.CurrentMediaId.Should().Be(SerieId);
        _sut.IsFinished.Should().BeFalse();
        await _js.DidNotReceive().InvokeAsync<IJSVoidResult>(
            "K7.AmbientTheme.fadeOut",
            Arg.Any<CancellationToken>(),
            Arg.Any<object?[]>());
    }

    [Test]
    public async Task Navigation_ShouldKeepFinishedContext_WhenBrowsingPersonsThenReturningToSerie()
    {
        var url = "https://server/api/medias/a/theme";
        await StartThemeAsync(SerieId, url);
        _sut.NotifyNaturalEnded();

        _sut.HandleLocationChanged($"https://app/persons/{PersonId}");
        _sut.HandleLocationChanged($"https://app/persons/{Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")}");
        _sut.HandleLocationChanged($"https://app/series/{SerieId}");
        await Task.Delay(80);

        await _sut.KeepOrStartAsync(SerieId, url, [1, 2, 3]);

        _sut.CurrentMediaId.Should().Be(SerieId);
        _sut.IsFinished.Should().BeTrue();
        await AssertPlayBytesCalledAsync(times: 1);
    }

    [Test]
    public async Task Navigation_ShouldStartTheme_WhenGoingFromPersonToOtherMedia()
    {
        await StartThemeAsync(SerieId, "https://server/api/medias/a/theme");
        _sut.HandleLocationChanged($"https://app/persons/{PersonId}");

        await StartThemeAsync(OtherMediaId, "https://server/api/medias/b/theme");

        _sut.CurrentMediaId.Should().Be(OtherMediaId);
        _sut.IsFinished.Should().BeFalse();
        await AssertPlayBytesCalledAsync(times: 2);
    }

    [Test]
    public async Task Navigation_ShouldFadeOut_WhenLeavingMediaContext()
    {
        await StartThemeAsync(SerieId, "https://server/api/medias/a/theme");

        _sut.HandleLocationChanged("https://app/");
        await Task.Delay(80);

        _sut.CurrentMediaId.Should().BeNull();
        _sut.IsFinished.Should().BeFalse();
        await _js.Received().InvokeAsync<IJSVoidResult>(
            "K7.AmbientTheme.fadeOut",
            Arg.Any<CancellationToken>(),
            Arg.Any<object?[]>());
    }

    [Test]
    public async Task Navigation_ShouldKeepTheme_WhenSerieToSeasonToEpisode()
    {
        await StartThemeAsync(SerieId, "https://server/api/medias/a/theme");

        _sut.HandleLocationChanged($"https://app/series/{SerieId}/seasons/1");
        _sut.HandleLocationChanged($"https://app/series/{SerieId}/seasons/1/episodes/2");
        await Task.Delay(80);

        _sut.CurrentMediaId.Should().Be(SerieId);
        await _js.DidNotReceive().InvokeAsync<IJSVoidResult>(
            "K7.AmbientTheme.fadeOut",
            Arg.Any<CancellationToken>(),
            Arg.Any<object?[]>());
    }

    [Test]
    public async Task FadeOutAsync_ShouldClearFinishedContext()
    {
        await StartThemeAsync(SerieId, "https://server/api/medias/a/theme");
        _sut.NotifyNaturalEnded();

        await _sut.FadeOutAsync(0.1);

        _sut.CurrentMediaId.Should().BeNull();
        _sut.IsFinished.Should().BeFalse();
    }

    [Test]
    public async Task InterruptAsync_ShouldKeepFinishedContext_WhenSameMedia()
    {
        var url = "https://server/api/medias/a/theme";
        await StartThemeAsync(SerieId, url);

        await _sut.InterruptAsync(SerieId);

        _sut.CurrentMediaId.Should().Be(SerieId);
        _sut.IsFinished.Should().BeTrue();

        await _sut.KeepOrStartAsync(SerieId, url, [9, 9, 9]);
        await AssertPlayBytesCalledAsync(times: 1);
        _sut.IsFinished.Should().BeTrue();
    }

    [Test]
    public async Task InterruptAsync_ShouldAllowOtherMediaTheme()
    {
        await StartThemeAsync(SerieId, "https://server/api/medias/a/theme");
        await _sut.InterruptAsync(SerieId);

        await StartThemeAsync(OtherMediaId, "https://server/api/medias/b/theme");

        _sut.CurrentMediaId.Should().Be(OtherMediaId);
        _sut.IsFinished.Should().BeFalse();
        await AssertPlayBytesCalledAsync(times: 2);
    }

    [Test]
    public async Task KeepOrStartAsync_ShouldHaltJs_WhenInterruptedDuringPlay()
    {
        var enteredPlay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _js.InvokeAsync<IJSVoidResult>(
                "K7.AmbientTheme.playBytes",
                Arg.Any<CancellationToken>(),
                Arg.Any<object?[]>())
            .Returns(_ =>
            {
                enteredPlay.TrySetResult();
                return new ValueTask<IJSVoidResult>(DelayedVoidResultAsync());
            });

        var start = StartThemeAsync(SerieId, "https://server/api/medias/a/theme");
        await enteredPlay.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await _sut.InterruptAsync(SerieId);
        await start;

        _sut.CurrentMediaId.Should().Be(SerieId);
        _sut.IsFinished.Should().BeTrue();
        await _js.Received().InvokeAsync<IJSVoidResult>(
            "K7.AmbientTheme.stop",
            Arg.Any<CancellationToken>(),
            Arg.Any<object?[]>());
    }

    [Test]
    public async Task Navigation_ShouldAllowRestart_AfterLeaveThenReturnToSerie()
    {
        var url = "https://server/api/medias/a/theme";
        await StartThemeAsync(SerieId, url);
        await _sut.InterruptAsync(SerieId);

        _sut.HandleLocationChanged("https://app/");
        await Task.Delay(80);

        _sut.CurrentMediaId.Should().BeNull();
        await StartThemeAsync(SerieId, url);

        _sut.CurrentMediaId.Should().Be(SerieId);
        _sut.IsFinished.Should().BeFalse();
        await AssertPlayBytesCalledAsync(times: 2);
    }

    private async Task StartThemeAsync(Guid mediaId, string url)
    {
        await _sut.KeepOrStartAsync(mediaId, url, [1, 2, 3, 4]);
    }

    private async Task AssertPlayBytesCalledAsync(int times)
    {
        await _js.Received(times).InvokeAsync<IJSVoidResult>(
            "K7.AmbientTheme.playBytes",
            Arg.Any<CancellationToken>(),
            Arg.Any<object?[]>());
    }

    private static async Task<IJSVoidResult> DelayedVoidResultAsync()
    {
        await Task.Delay(80);
        return Substitute.For<IJSVoidResult>();
    }

    private sealed class StubNavigationManager : NavigationManager
    {
        public StubNavigationManager() => Initialize("https://app/", "https://app/");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }
}
