using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7SaveIndicatorTests
{
    [TestCase(SettingsSaveState.Saving, "ph-circle-notch", "AutoSaveSaving")]
    [TestCase(SettingsSaveState.Saved, "ph-check-circle", "AutoSaveSaved")]
    [TestCase(SettingsSaveState.Failed, "ph-warning-circle", "AutoSaveFailed")]
    public void Render_ShouldShowTheIconAndText_WhenStateIsSet(SettingsSaveState state, string iconClass, string textKey)
    {
        // Arrange
        using var ctx = CreateContext();

        // Act
        var cut = ctx.Render<K7SaveIndicator>(p => p.Add(c => c.State, state));

        // Assert
        var indicator = cut.Find(".k7-save-indicator");
        indicator.TextContent.Should().Contain(textKey);
        indicator.QuerySelector(".k7-save-indicator__icon i")!.ClassList.Should().Contain(iconClass);
    }

    [Test]
    public void Render_ShouldKeepAnEmptyLiveRegion_WhenIdle()
    {
        // Arrange
        using var ctx = CreateContext();

        // Act
        var cut = ctx.Render<K7SaveIndicator>();

        // Assert
        var indicator = cut.Find(".k7-save-indicator");
        indicator.GetAttribute("aria-live").Should().Be("polite");
        indicator.TextContent.Trim().Should().BeEmpty();
    }

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(localizer);
        return ctx;
    }
}
