using K7.Clients.Shared.UI.Components;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class MediaHeroSkeletonTests
{
    [Test]
    public void Render_ShouldUseDefaultLayoutClass_WhenNoLayoutFlag()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<MediaHeroSkeleton>(p => p.Add(c => c.ShowCast, false));

        cut.Find(".media-hero-skeleton").ClassList.Should().NotContain("media-hero-skeleton--detail");
        cut.Find(".media-hero-skeleton").ClassList.Should().NotContain("media-hero-skeleton--portrait");
        cut.Find(".media-hero-skeleton").ClassList.Should().NotContain("media-hero-skeleton--season");
        cut.FindAll(".media-hero-skeleton__play").Should().ContainSingle();
    }

    [Test]
    public void Render_ShouldUseDetailLayoutClass_WhenDetailLayout()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<MediaHeroSkeleton>(p => p
            .Add(c => c.DetailLayout, true)
            .Add(c => c.ShowCast, false));

        cut.Find(".media-hero-skeleton").ClassList.Should().Contain("media-hero-skeleton--detail");
        cut.FindAll(".media-hero-skeleton__play").Should().ContainSingle();
    }

    [Test]
    public void Render_ShouldUsePortraitLayout_WhenPortraitLayout()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<MediaHeroSkeleton>(p => p
            .Add(c => c.PortraitLayout, true)
            .Add(c => c.ShowCast, false));

        cut.Find(".media-hero-skeleton").ClassList.Should().Contain("media-hero-skeleton--portrait");
        cut.FindAll(".media-hero-skeleton__name").Should().ContainSingle();
        cut.FindAll(".media-hero-skeleton__play").Should().BeEmpty();
    }

    [Test]
    public void Render_ShouldUseSeasonLayout_WhenSeasonLayout()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<MediaHeroSkeleton>(p => p
            .Add(c => c.SeasonLayout, true)
            .Add(c => c.ShowCast, false));

        cut.Find(".media-hero-skeleton").ClassList.Should().Contain("media-hero-skeleton--season");
        cut.FindAll(".media-hero-skeleton__season-logo").Should().ContainSingle();
        cut.FindAll(".media-hero-skeleton__episode-row").Should().HaveCount(6);
        cut.FindAll(".media-hero-skeleton__play").Should().BeEmpty();
    }
}
