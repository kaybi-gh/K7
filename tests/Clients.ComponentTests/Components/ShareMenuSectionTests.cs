using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Shares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class ShareMenuSectionTests
{
    private static BunitContext CreateContext(MediaSharingAvailabilityDto availability)
    {
        var ctx = new BunitContext();

        var sharingAccess = Substitute.For<IMediaSharingAccess>();
        sharingAccess.GetAsync(Arg.Any<CancellationToken>()).Returns(availability);
        ctx.Services.AddSingleton(sharingAccess);
        ctx.Services.AddSingleton(Substitute.For<IK7DialogService>());

        var localizer = Substitute.For<IStringLocalizer<ShareMenuSection>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(localizer);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        return ctx;
    }

    private static IRenderedComponent<ShareMenuSection> Render(BunitContext ctx, Guid mediaId) =>
        ctx.Render<ShareMenuSection>(parameters => parameters
            .Add(p => p.MediaId, mediaId)
            .Add(p => p.MediaType, MediaType.Movie)
            .Add(p => p.MediaTitle, "Dune"));

    [Test]
    public void Render_ShouldShowShareEntry_WhenUserCanShare()
    {
        using var ctx = CreateContext(new MediaSharingAvailabilityDto { Enabled = true, CanShare = true });

        var cut = Render(ctx, Guid.NewGuid());

        cut.Markup.Should().Contain("Share");
    }

    [Test]
    public void Render_ShouldShowNothing_WhenUserLacksTheCapability()
    {
        using var ctx = CreateContext(new MediaSharingAvailabilityDto { Enabled = true, CanShare = false });

        var cut = Render(ctx, Guid.NewGuid());

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Test]
    public void Render_ShouldShowNothing_WhenPublicSharesAreDisabled()
    {
        using var ctx = CreateContext(new MediaSharingAvailabilityDto { Enabled = false, CanShare = true });

        var cut = Render(ctx, Guid.NewGuid());

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Test]
    public void Render_ShouldShowNothing_WhenMediaIdIsEmpty()
    {
        using var ctx = CreateContext(new MediaSharingAvailabilityDto { Enabled = true, CanShare = true });

        var cut = Render(ctx, Guid.Empty);

        cut.Markup.Trim().Should().BeEmpty();
    }
}
