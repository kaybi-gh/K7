using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Helpers;

public class CustomNavPathTests
{
    [Test]
    public void IsAdminPath_ShouldDetectAdminRoutes()
    {
        CustomNavPath.IsAdminPath("/admin").Should().BeTrue();
        CustomNavPath.IsAdminPath("/admin/navigation").Should().BeTrue();
        CustomNavPath.IsAdminPath("/k7/admin/dashboard").Should().BeTrue();
        CustomNavPath.IsAdminPath("/settings/navigation").Should().BeFalse();
        CustomNavPath.IsAdminPath("/explore").Should().BeFalse();
    }

    [Test]
    public void ShouldShowBar_ShouldBeTrue_OnAdmin_WhenSettingsIsEnabled()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.Bar,
            BarShowIcons = true,
            ShowBarOnSettings = true,
            Items =
            [
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.AppRoute,
                    Route = "/search"
                }
            ]
        };

        CustomNavPath.ShouldShowBar(layout, "/admin/navigation", DeviceType.Desktop).Should().BeTrue();
        CustomNavPath.ShouldShowBar(layout, "/admin/dashboard", DeviceType.Desktop).Should().BeTrue();
    }
}
