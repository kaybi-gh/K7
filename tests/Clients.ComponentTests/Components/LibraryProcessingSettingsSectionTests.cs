using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class LibraryProcessingSettingsSectionTests
{
    [Test]
    public void Toggle_ShouldNotifyParent_WhenSubtitlePreExtractionChanges()
    {
        using var ctx = new BunitContext();
        var localizer = Substitute.For<IStringLocalizer<LibraryProcessingSettingsSection>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(localizer);

        bool? notified = null;
        var cut = ctx.Render<LibraryProcessingSettingsSection>(parameters => parameters
            .Add(p => p.MediaType, LibraryMediaType.Movie)
            .Add(p => p.SubtitlePreExtractionEnabled, false)
            .Add(p => p.SubtitlePreExtractionEnabledChanged, (bool value) => notified = value));

        cut.Find("label[aria-label='SubtitlePreExtraction'] input").Change(true);

        notified.Should().BeTrue();
    }
}
