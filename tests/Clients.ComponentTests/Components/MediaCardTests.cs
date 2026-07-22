using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
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

        var cut = ctx.Render(BuildCardWithHost(model, overlayEnabled: true, href: "/movies/1"));

        // Act
        var activator = cut.Find(".media-card-menu-trigger");
        await cut.InvokeAsync(() => activator.Click());

        // Assert
        cut.FindAll(".k7-menu-item").Should().ContainSingle(item => item.TextContent.Contains("Play"));
    }

    [Test]
    public async Task OnKeyUp_ShouldNotNavigate_WhenStrayKeyUpArrivesAfterKeyboardLongPressMenuCloses()
    {
        // Arrange
        using var ctx = CreateContext();
        var model = CreateModel();
        var contextMenu = ctx.Services.GetRequiredService<IMediaCardContextMenuService>();

        var cut = ctx.Render(BuildCardWithHost(model, overlayEnabled: true, href: "/movies/1"));

        var card = cut.FindComponent<MediaCard>();
        var link = cut.Find(".media-card-link");

        // Holding Enter for a long-press opens the context menu. The physical
        // keyup that ends this hold is swallowed by navigation.js and never
        // reaches OnKeyUp - simulated here by invoking the JS-triggered open
        // callback directly instead of the real 600ms timer.
        await link.KeyDownAsync("Enter");
        await cut.InvokeAsync(() => card.Instance.OpenContextMenuFromLongPressAsync());
        cut.Find(".k7-menu-dropdown--open").Should().NotBeNull();

        // Closing the menu (e.g. pressing Enter on the close button) resets
        // the long-press state and restores focus to the media card link.
        var closeButton = cut.Find(".k7-menu-close");
        await cut.InvokeAsync(() => closeButton.Click());
        contextMenu.Current.Should().BeNull();

        var navigationManager = ctx.Services.GetRequiredService<NavigationManager>();
        var uriBeforeStrayKeyUp = navigationManager.Uri;

        // Act - a stray keyup from the same Enter press lands back on the
        // media card link once focus is restored there.
        await link.KeyUpAsync("Enter");

        // Assert - it must not be misread as the release of a genuine short
        // press and triggers navigation.
        navigationManager.Uri.Should().Be(uriBeforeStrayKeyUp);
    }

    private static RenderFragment BuildCardWithHost(
        MediaCardViewModel model,
        bool overlayEnabled,
        string href) => builder =>
    {
        builder.OpenComponent<MediaCardContextMenuHost>(0);
        builder.CloseComponent();

        builder.OpenComponent<MediaCard>(1);
        builder.AddAttribute(2, nameof(MediaCard.Model), model);
        builder.AddAttribute(3, nameof(MediaCard.OverlayEnabled), overlayEnabled);
        builder.AddAttribute(4, nameof(MediaCard.Href), href);
        builder.CloseComponent();
    };

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
        ctx.Services.AddSingleton<MediaCardContextMenuService>();
        ctx.Services.AddSingleton<IMediaCardContextMenuService>(sp => sp.GetRequiredService<MediaCardContextMenuService>());
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
