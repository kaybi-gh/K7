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

    private static IAudioPlayerService CreateAudioService()
    {
        var audio = Substitute.For<IAudioPlayerService>();
        audio.Repeat.Returns(RepeatMode.Off);
        return audio;
    }

    private static AudioQueueItem CreateTrack() => new()
    {
        IndexedFileId = Guid.NewGuid(),
        MediaId = Guid.NewGuid(),
        Title = "Test Track",
        Artist = "Test Artist",
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

        var sharedLocalizer = Substitute.For<IStringLocalizer<SharedResource>>();
        sharedLocalizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(sharedLocalizer);

        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        return ctx;
    }
}
