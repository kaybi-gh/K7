using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class RatingStarsTests
{
    [Test]
    public async Task PointerUp_ShouldUpdateOtherInstance_WhenSameMediaId()
    {
        var mediaId = Guid.NewGuid();
        var sync = new UserRatingSync([]);
        using var ctx = CreateContext(sync, canRate: true);
        SetupBounds(ctx, width: 110);

        var source = ctx.Render<RatingStars>(p => p
            .Add(x => x.MediaId, mediaId)
            .Add(x => x.Value, 4)
            .Add(x => x.Size, "md"));
        var target = ctx.Render<RatingStars>(p => p
            .Add(x => x.MediaId, mediaId)
            .Add(x => x.Value, 4)
            .Add(x => x.Size, "md"));

        await PointerRateAsync(source, clientX: 105);

        target.WaitForAssertion(() =>
            target.FindAll(".rating-star.star--filled").Should().HaveCount(5));

        sync.TryGet(mediaId, out var cached).Should().BeTrue();
        cached.Should().Be(10);
    }

    [Test]
    public void Render_ShouldPreferCachedRating_OverStaleParameter()
    {
        var mediaId = Guid.NewGuid();
        var sync = new UserRatingSync([]);
        sync.Set(mediaId, 10);
        using var ctx = CreateContext(sync, canRate: true);

        var cut = ctx.Render<RatingStars>(p => p
            .Add(x => x.MediaId, mediaId)
            .Add(x => x.Value, 2)
            .Add(x => x.Size, "md"));

        cut.FindAll(".rating-star.star--filled").Should().HaveCount(5);
    }

    [Test]
    public void Render_ShouldShowHalfStar_WhenValueIsOdd()
    {
        var mediaId = Guid.NewGuid();
        using var ctx = CreateContext(new UserRatingSync([]), canRate: true);

        var cut = ctx.Render<RatingStars>(p => p
            .Add(x => x.MediaId, mediaId)
            .Add(x => x.Value, 7)
            .Add(x => x.Size, "md"));

        cut.FindAll(".rating-star.star--filled").Should().HaveCount(3);
        cut.FindAll(".rating-star.star--half").Should().HaveCount(1);
    }

    [Test]
    public async Task PointerUp_ShouldClearRating_WhenDraggedToStart()
    {
        var mediaId = Guid.NewGuid();
        var sync = new UserRatingSync([]);
        using var ctx = CreateContext(sync, canRate: true);
        SetupBounds(ctx, width: 110);

        var cut = ctx.Render<RatingStars>(p => p
            .Add(x => x.MediaId, mediaId)
            .Add(x => x.Value, 8)
            .Add(x => x.Size, "md"));

        await PointerRateAsync(cut, clientX: 2);

        cut.FindAll(".rating-star.star--filled").Should().BeEmpty();
        cut.FindAll(".rating-star.star--half").Should().BeEmpty();
        sync.TryGet(mediaId, out var cached).Should().BeTrue();
        cached.Should().BeNull();
    }

    private static async Task PointerRateAsync(IRenderedComponent<RatingStars> cut, double clientX)
    {
        var args = new PointerEventArgs
        {
            Button = 0,
            ClientX = clientX,
            PointerType = "mouse"
        };
        await cut.Find(".rating-stars").TriggerEventAsync("onpointerdown", args);
        await cut.Find(".rating-stars").TriggerEventAsync("onpointerup", args);
    }

    private static void SetupBounds(BunitContext ctx, double width) =>
        ctx.JSInterop.Setup<RatingPointerRect>("K7.getBoundingRect", _ => true)
            .SetResult(new RatingPointerRect(0, 0, width, 20));

    private static BunitContext CreateContext(IUserRatingSync sync, bool canRate)
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(sync);

        var featureAccess = Substitute.For<IFeatureAccessService>();
        featureAccess.HasCapabilityAsync(Capability.CanRate).Returns(canRate);
        ctx.Services.AddSingleton(featureAccess);
        ctx.Services.AddSingleton(Substitute.For<IRatingService>());
        ctx.Services.AddSingleton(Substitute.For<IConnectivityService>());
        ctx.Services.AddSingleton(Substitute.For<IPlaybackJournal>());
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }
}
