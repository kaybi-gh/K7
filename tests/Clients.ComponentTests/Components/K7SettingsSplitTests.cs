using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7SettingsSplitTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void Render_ShouldFlagTheRoot_WhenDetailIsOpen(bool detailOpen)
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7SettingsSplit>(p => p
            .Add(c => c.IndexLabel, "Video playback")
            .Add(c => c.DetailTitle, "Subtitles")
            .Add(c => c.BackHref, "/settings/video-playback")
            .Add(c => c.DetailOpen, detailOpen)
            .Add(c => c.Index, "<span>index</span>")
            .Add(c => c.Detail, "<span>detail</span>"));

        // Assert
        cut.Find(".k7-settings-split").ClassList.Contains("k7-settings-split--detail-open")
            .Should().Be(detailOpen);
    }

    [Test]
    public void Render_ShouldLinkBackToTheIndex()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7SettingsSplit>(p => p
            .Add(c => c.IndexLabel, "Video playback")
            .Add(c => c.DetailTitle, "Subtitles")
            .Add(c => c.BackHref, "/settings/video-playback")
            .Add(c => c.Index, "<span>index</span>")
            .Add(c => c.Detail, "<span>detail</span>"));

        // Assert
        var back = cut.Find(".k7-settings-split__back");
        back.GetAttribute("href").Should().Be("/settings/video-playback");
        back.TextContent.Should().Contain("Video playback");
        cut.Find(".k7-settings-split__title").TextContent.Should().Be("Subtitles");
    }

    [Test]
    public void Render_ShouldAddASecondPanel_WhenExtraIsSet()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var alone = ctx.Render<K7SettingsSplit>(p => p
            .Add(c => c.IndexLabel, "Interface")
            .Add(c => c.DetailTitle, "Media pages")
            .Add(c => c.BackHref, "/settings/interface")
            .Add(c => c.Index, "<span>index</span>")
            .Add(c => c.Detail, "<span>detail</span>"));
        var both = ctx.Render<K7SettingsSplit>(p => p
            .Add(c => c.IndexLabel, "Interface")
            .Add(c => c.DetailTitle, "Media pages")
            .Add(c => c.BackHref, "/settings/interface")
            .Add(c => c.Index, "<span>index</span>")
            .Add(c => c.Detail, "<span>detail</span>")
            .Add(c => c.ExtraTitle, "Theme songs")
            .Add(c => c.Extra, "<span>extra</span>"));

        // Assert
        alone.FindAll(".k7-settings-split__panel").Should().ContainSingle();

        both.FindAll(".k7-settings-split__panel").Should().HaveCount(2);
        var extra = both.Find(".k7-settings-split__panel--extra");
        extra.QuerySelector(".k7-settings-split__title")!.TextContent.Should().Be("Theme songs");
        extra.TextContent.Should().Contain("extra");
        // The back link belongs to the sub-page, not to each panel.
        both.FindAll(".k7-settings-split__back").Should().ContainSingle();
    }

    [Test]
    public void Render_ShouldPutTheSecondPanelFirst_WithTheBackLink_WhenExtraFirstIsSet()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7SettingsSplit>(p => p
            .Add(c => c.IndexLabel, "Interface")
            .Add(c => c.DetailTitle, "Appearance")
            .Add(c => c.BackHref, "/settings/interface")
            .Add(c => c.Index, "<span>index</span>")
            .Add(c => c.Detail, "<span>detail</span>")
            .Add(c => c.ExtraTitle, "Language")
            .Add(c => c.Extra, "<span>extra</span>")
            .Add(c => c.ExtraFirst, true));

        // Assert
        var panels = cut.FindAll(".k7-settings-split__panel");
        panels.Should().HaveCount(2);
        panels[0].ClassList.Should().Contain("k7-settings-split__panel--extra");
        panels[0].QuerySelector(".k7-settings-split__title")!.TextContent.Should().Be("Language");
        // The way back to the index stays at the top of the sub-page.
        panels[0].QuerySelector(".k7-settings-split__back").Should().NotBeNull();
        panels[1].QuerySelector(".k7-settings-split__back").Should().BeNull();
        panels[1].QuerySelector(".k7-settings-split__title")!.TextContent.Should().Be("Appearance");
    }

    [Test]
    public void Render_ShouldShowTheSaveIndicatorInTheFooter_WhenSaveStateIsSet()
    {
        // Arrange
        using var ctx = new BunitContext();
        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(localizer);

        // Act
        var cut = ctx.Render<K7SettingsSplit>(p => p
            .Add(c => c.IndexLabel, "Video playback")
            .Add(c => c.DetailTitle, "Subtitles")
            .Add(c => c.BackHref, "/settings/video-playback")
            .Add(c => c.SaveState, SettingsSaveState.Saved)
            .Add(c => c.Index, "<span>index</span>")
            .Add(c => c.Detail, "<span>detail</span>"));

        // Assert
        cut.Find(".k7-settings-split__footer .k7-save-indicator").TextContent
            .Should().Contain("AutoSaveSaved");
    }
}
