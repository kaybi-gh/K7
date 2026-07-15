using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class MediaCardTests
{
    [Test]
    public void Render_ShouldDisplayAdditionalInformations_InFooter()
    {
        // Arrange
        using var ctx = CreateContext();
        var model = new MediaCardViewModel
        {
            Id = Guid.NewGuid().ToString(),
            Kind = MediaCardKind.Poster,
            MediaType = MediaType.Movie,
            Title = "Test Movie",
            AdditionalInformations = "2010"
        };

        // Act
        var cut = ctx.Render<MediaCard>(p => p
            .Add(c => c.Model, model)
            .Add(c => c.FooterVisible, true));

        // Assert
        cut.Find(".media-card-subtitle").TextContent.Should().Be("2010");
    }

    [Test]
    public void Render_ShouldEnableLongPress_WhenOverlayEnabled()
    {
        // Arrange
        using var ctx = CreateContext();
        var model = CreateModel();

        // Act
        var cut = ctx.Render<MediaCard>(p => p
            .Add(c => c.Model, model)
            .Add(c => c.OverlayEnabled, true)
            .Add(c => c.Href, "/movies/1"));

        // Assert
        cut.Find("[data-longpress='true']").Should().NotBeNull();
        cut.Find("[data-longpress-target]").Should().NotBeNull();
    }

    [Test]
    public async Task Render_ShouldShowPlayMenuItem_WhenOverlayEnabledAndMenuOpened()
    {
        // Arrange
        using var ctx = CreateContext();
        var model = CreateModel();

        var cut = ctx.Render<MediaCard>(p => p
            .Add(c => c.Model, model)
            .Add(c => c.OverlayEnabled, true)
            .Add(c => c.Href, "/movies/1"));

        // Act
        var activator = cut.Find(".k7-menu-activator-inner");
        await cut.InvokeAsync(() => activator.Click());

        // Assert
        cut.FindAll(".k7-menu-item").Should().ContainSingle(item => item.TextContent.Contains("Play"));
    }

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());

        var featureAccess = Substitute.For<IFeatureAccessService>();
        featureAccess.HasCapabilityAsync(Capability.CanRate).Returns(false);
        featureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist).Returns(false);
        ctx.Services.AddSingleton(featureAccess);
        ctx.Services.AddSingleton(Substitute.For<IMediaService>());
        ctx.Services.AddSingleton(Substitute.For<IK7DialogService>());
        ctx.Services.AddSingleton(Substitute.For<IK7Snackbar>());
        ctx.Services.AddSingleton(new MediaCacheStore());
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var contextMenuLocalizer = Substitute.For<IStringLocalizer<MediaCardContextMenu>>();
        contextMenuLocalizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(contextMenuLocalizer);

        var sharedLocalizer = Substitute.For<IStringLocalizer<SharedResource>>();
        sharedLocalizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(sharedLocalizer);

        var reviewLocalizer = Substitute.For<IStringLocalizer<MediaReviewDialog>>();
        reviewLocalizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(reviewLocalizer);

        return ctx;
    }

    private static MediaCardViewModel CreateModel() => new()
    {
        Id = Guid.NewGuid().ToString(),
        Kind = MediaCardKind.Poster,
        MediaType = MediaType.Movie,
        Title = "Test Movie"
    };
}
