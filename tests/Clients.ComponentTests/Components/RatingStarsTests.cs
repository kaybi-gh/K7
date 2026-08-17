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
    public async Task Click_ShouldUpdateOtherInstance_WhenSameMediaId()
    {
        var mediaId = Guid.NewGuid();
        var sync = new UserRatingSync([]);
        using var ctx = CreateContext(sync, canRate: true);

        var source = ctx.Render<RatingStars>(p => p
            .Add(x => x.MediaId, mediaId)
            .Add(x => x.Value, 4)
            .Add(x => x.Size, "md"));
        var target = ctx.Render<RatingStars>(p => p
            .Add(x => x.MediaId, mediaId)
            .Add(x => x.Value, 4)
            .Add(x => x.Size, "md"));

        await source.FindAll(".rating-star")[4].ClickAsync(new MouseEventArgs());
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
