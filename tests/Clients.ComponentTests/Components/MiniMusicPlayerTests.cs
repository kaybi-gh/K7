using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Players;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class MiniMusicPlayerTests
{
    [SetUp]
    public void SetUp() => AppLifecycleGate.SetForeground(true);

    [TearDown]
    public void TearDown() => AppLifecycleGate.SetForeground(true);

    [Test]
    public void Render_ShouldDisplayTrackInfoAndFormattedTime_WhenTrackIsActive()
    {
        // Arrange
        var audio = CreateAudioService();
        var track = CreateTrack();
        audio.IsVisible.Returns(true);
        StubTracks(audio, track);
        audio.CurrentTime = 65;
        audio.Duration = 200;
        audio.PlaybackState = PlaybackState.Paused;

        using var ctx = CreateContext(audio);

        // Act
        var cut = ctx.Render<MiniMusicPlayer>();

        // Assert
        cut.Markup.Should().Contain("Test Track").And.Contain("Test Artist");
        cut.Find(".mini-player-time").TextContent.Should().Contain("1:05").And.Contain("3:20");
    }

    [Test]
    public void Render_ShouldRenderNothing_WhenPlayerIsNotVisible()
    {
        // Arrange
        var audio = CreateAudioService();
        audio.IsVisible.Returns(false);
        StubTracks(audio, null);

        using var ctx = CreateContext(audio);

        // Act
        var cut = ctx.Render<MiniMusicPlayer>();

        // Assert
        cut.Markup.Should().BeEmpty();
    }

    [Test]
    public void TrackChange_ShouldNotRender_WhenHostIsBackgrounded()
    {
        var audio = CreateAudioService();
        var first = CreateTrack();
        var second = CreateTrack("Incoming Track", "Incoming Artist");
        audio.IsVisible.Returns(true);
        StubTracks(audio, first);
        audio.PlaybackState = PlaybackState.Playing;

        using var ctx = CreateContext(audio);
        var cut = ctx.Render<MiniMusicPlayer>();
        cut.Markup.Should().Contain("Test Track");

        AppLifecycleGate.SetForeground(false);
        StubTracks(audio, second);
        audio.CurrentTrackChanged += Raise.Event<Action<AudioQueueItem?>>(second);

        cut.Markup.Should().Contain("Test Track");
        cut.Markup.Should().NotContain("Incoming Track");

        AppLifecycleGate.SetForeground(true);
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Incoming Track"));
    }

    [Test]
    public void Render_ShouldShowDisplayedTrack_WhenPlayingTrackHasAlreadyAdvanced()
    {
        var audio = CreateAudioService();
        var outgoing = CreateTrack("Outgoing Track", "Outgoing Artist");
        var incoming = CreateTrack("Incoming Track", "Incoming Artist");
        audio.IsVisible.Returns(true);
        audio.CurrentPlayingTrack.Returns(incoming);
        audio.CurrentDisplayedTrack.Returns(outgoing);
        audio.CurrentTrack.Returns(incoming);
        audio.PlaybackState = PlaybackState.Playing;

        using var ctx = CreateContext(audio);
        var cut = ctx.Render<MiniMusicPlayer>();

        cut.Markup.Should().Contain("Outgoing Track").And.Contain("Outgoing Artist");
        cut.Markup.Should().NotContain("Incoming Track");
    }

    [Test]
    public async Task Rating_ShouldPersistDisplayedTrackOnly_WhenPlayingTrackHasAlreadyAdvanced()
    {
        var audio = CreateAudioService();
        var outgoing = CreateTrack("Outgoing Track");
        outgoing.UserRating = 2;
        var incoming = CreateTrack("Incoming Track");
        incoming.UserRating = 8;
        audio.IsVisible.Returns(true);
        audio.CurrentPlayingTrack.Returns(incoming);
        audio.CurrentDisplayedTrack.Returns(outgoing);
        audio.CurrentTrack.Returns(incoming);

        var ratingService = Substitute.For<IRatingService>();
        var connectivity = Substitute.For<IConnectivityService>();
        connectivity.IsOnline.Returns(true);

        using var ctx = CreateContext(audio, canRate: true, ratingService, connectivity);
        ctx.JSInterop.Setup<RatingPointerRect>("K7.getBoundingRect", _ => true)
            .SetResult(new RatingPointerRect(0, 0, 110, 20));

        var cut = ctx.Render<MiniMusicPlayer>();
        var args = new PointerEventArgs
        {
            Button = 0,
            ClientX = 105,
            PointerType = "mouse"
        };
        await cut.Find(".rating-stars").TriggerEventAsync("onpointerdown", args);
        await cut.Find(".rating-stars").TriggerEventAsync("onpointerup", args);

        outgoing.UserRating.Should().Be(10);
        incoming.UserRating.Should().Be(8);
        await ratingService.Received(1).RateMediaAsync(outgoing.MediaId, 10);
        await ratingService.DidNotReceive().RateMediaAsync(incoming.MediaId, Arg.Any<int>());
    }

    private static IAudioPlayerService CreateAudioService()
    {
        var audio = Substitute.For<IAudioPlayerService>();
        audio.Repeat.Returns(RepeatMode.Off);
        return audio;
    }

    private static void StubTracks(IAudioPlayerService audio, AudioQueueItem? track)
    {
        audio.CurrentPlayingTrack.Returns(track);
        audio.CurrentDisplayedTrack.Returns(track);
        audio.CurrentTrack.Returns(track);
    }

    private static AudioQueueItem CreateTrack(string title = "Test Track", string artist = "Test Artist") => new()
    {
        IndexedFileId = Guid.NewGuid(),
        MediaId = Guid.NewGuid(),
        Title = title,
        Artist = artist,
        AlbumTitle = "Test Album"
    };

    private static BunitContext CreateContext(
        IAudioPlayerService audio,
        bool canRate = false,
        IRatingService? ratingService = null,
        IConnectivityService? connectivity = null)
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(audio);

        var deviceService = Substitute.For<IDeviceService>();
        deviceService.GetDeviceTypeAsync().Returns(DeviceType.Desktop);
        ctx.Services.AddSingleton(deviceService);

        var featureAccess = Substitute.For<IFeatureAccessService>();
        featureAccess.HasCapabilityAsync(Capability.CanRate).Returns(canRate);
        ctx.Services.AddSingleton(featureAccess);

        ctx.Services.AddSingleton(ratingService ?? Substitute.For<IRatingService>());
        ctx.Services.AddSingleton(connectivity ?? Substitute.For<IConnectivityService>());
        ctx.Services.AddSingleton(Substitute.For<IPlaybackJournal>());
        ctx.Services.AddSingleton(Substitute.For<ILocalUserService>());
        ctx.Services.AddSingleton<IUserRatingSync, UserRatingSync>();

        var sharedLocalizer = Substitute.For<IStringLocalizer<SharedResource>>();
        sharedLocalizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(sharedLocalizer);

        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        return ctx;
    }
}
