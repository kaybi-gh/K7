using K7.Clients.Shared.UI.Components;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class MediaPageBackdropTests
{
    private const string ModulePath = "./_content/K7.Clients.Shared.UI/js/mediaPageBackdrop.js";

    [Test]
    public void ParentRerender_ShouldNotPickHeroUrlsAgain_WhenUrlsAreUnchanged()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = ctx.JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<string?>("pickHeroImageUrl", _ => true).SetResult("https://img.test/a.jpg");

        var cut = ctx.Render<MediaPageBackdrop>(p => p
            .Add(x => x.ImageUrl, "https://img.test/a.jpg")
            .Add(x => x.ScrollFadeEnabled, false));
        cut.WaitForAssertion(() => PickCount(module).Should().BeGreaterThan(0));
        var afterFirstRender = PickCount(module);

        cut.Render(p => p.Add(x => x.ImageUrl, "https://img.test/a.jpg"));
        cut.Render(p => p.Add(x => x.ImageUrl, "https://img.test/a.jpg"));

        PickCount(module).Should().Be(afterFirstRender);
    }

    [Test]
    public void ParentRerender_ShouldPickHeroUrlsAgain_WhenImageUrlChanges()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = ctx.JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<string?>("pickHeroImageUrl", _ => true).SetResult("https://img.test/a.jpg");

        var cut = ctx.Render<MediaPageBackdrop>(p => p
            .Add(x => x.ImageUrl, "https://img.test/a.jpg")
            .Add(x => x.ScrollFadeEnabled, false));
        cut.WaitForAssertion(() => PickCount(module).Should().BeGreaterThan(0));
        var afterFirstRender = PickCount(module);

        cut.Render(p => p.Add(x => x.ImageUrl, "https://img.test/b.jpg"));

        cut.WaitForAssertion(() => PickCount(module).Should().BeGreaterThan(afterFirstRender));
    }

    private static int PickCount(BunitJSModuleInterop module) =>
        module.Invocations["pickHeroImageUrl"].Count;
}
