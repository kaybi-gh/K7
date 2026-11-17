using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class InterfaceSettingsSplitTests
{
    [Test]
    public void Index_ShouldListTheSameSubPagesInTheSameOrder_ForPersonalSettingsAndServerDefaults()
    {
        var personal = SubPages(admin: false, InterfaceSettingsSections.SettingsBasePath);
        var server = SubPages(admin: true, InterfaceSettingsSections.AdminBasePath);

        server.Should().Equal(personal);
        personal.Should().Equal(
            InterfaceSettingsSections.Appearance,
            InterfaceSettingsSections.Navigation,
            InterfaceSettingsSections.Home,
            InterfaceSettingsSections.MediaPages,
            InterfaceSettingsSections.Cards,
            InterfaceSettingsSections.AudioPlayer,
            InterfaceSettingsSections.VideoPlayer);
    }

    [Test]
    public void Index_ShouldHaveNoEntryForThemeSongs()
    {
        // They are a second panel of the media pages, on both sides.
        SubPages(admin: true, InterfaceSettingsSections.AdminBasePath)
            .Should().NotContain(InterfaceSettingsSections.ThemeSongs);
    }

    private static List<string> SubPages(bool admin, string basePath)
    {
        using var ctx = new BunitContext();
        ctx.Services.AddLocalization();
        ctx.Services.AddSingleton(Substitute.For<IUserAdminService>());
        ctx.Services.AddSingleton(Substitute.For<IFeatureAccessService>());

        var cut = ctx.Render<InterfaceSettingsSplit>(p => p
            .Add(c => c.Section, InterfaceSettingsSections.Appearance)
            .Add(c => c.Admin, admin)
            .AddChildContent("<span>form</span>"));

        return [.. cut.FindAll(".k7-settings-nav-item")
            .Select(item => item.GetAttribute("href")!)
            .Select(href => href[(basePath.Length + 1)..])];
    }
}
