using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components.Players;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
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
        audio.CurrentTrack.Returns(track);
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
        audio.CurrentTrack.Returns((AudioQueueItem?)null);

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
        audio.CurrentTrack.Returns(first);
        audio.PlaybackState = PlaybackState.Playing;

        using var ctx = CreateContext(audio);
        var cut = ctx.Render<MiniMusicPlayer>();
        cut.Markup.Should().Contain("Test Track");

        AppLifecycleGate.SetForeground(false);
        audio.CurrentTrack.Returns(second);
        audio.CurrentTrackChanged += Raise.Event<Action<AudioQueueItem?>>(second);

        cut.Markup.Should().Contain("Test Track");
        cut.Markup.Should().NotContain("Incoming Track");

        AppLifecycleGate.SetForeground(true);
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Incoming Track"));
    }

    private static IAudioPlayerService CreateAudioService()
    {
        var audio = Substitute.For<IAudioPlayerService>();
        audio.Repeat.Returns(RepeatMode.Off);
        return audio;
    }

    private static AudioQueueItem CreateTrack(string title = "Test Track", string artist = "Test Artist") => new()
    {
        IndexedFileId = Guid.NewGuid(),
        MediaId = Guid.NewGuid(),
        Title = title,
        Artist = artist,
        AlbumTitle = "Test Album"
    };

    private static BunitContext CreateContext(IAudioPlayerService audio)
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(audio);

        var deviceService = Substitute.For<IDeviceService>();
        deviceService.GetDeviceTypeAsync().Returns(DeviceType.Desktop);
        ctx.Services.AddSingleton(deviceService);

        var featureAccess = Substitute.For<IFeatureAccessService>();
        featureAccess.HasCapabilityAsync(Capability.CanRate).Returns(false);
        ctx.Services.AddSingleton(featureAccess);

        ctx.Services.AddSingleton(Substitute.For<IRatingService>());
        ctx.Services.AddSingleton(Substitute.For<IConnectivityService>());
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
