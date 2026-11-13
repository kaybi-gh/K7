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
