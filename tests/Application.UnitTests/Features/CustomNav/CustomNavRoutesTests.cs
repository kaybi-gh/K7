using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.CustomNav;

public class CustomNavRoutesTests
{
    [Test]
    public void TryNormalize_ShouldAcceptAllowlistedPath()
    {
        CustomNavRoutes.TryNormalize("/My-Space/Playlists", out var normalized).Should().BeTrue();
        normalized.Should().Be("/my-space/playlists");
    }

    [Test]
    public void TryNormalize_ShouldAcceptHitParade()
    {
        CustomNavRoutes.TryNormalize("/my-space/hit-parade", out var normalized).Should().BeTrue();
        normalized.Should().Be("/my-space/hit-parade");
        CustomNavRoutes.Find("/my-space/hit-parade")!.LabelKey.Should().Be("RouteHitParade");
    }

    [Test]
    public void TryNormalize_ShouldRejectExternalUrl()
    {
        CustomNavRoutes.TryNormalize("https://example.com/search", out _).Should().BeFalse();
    }

    [Test]
    public void TryNormalize_ShouldMapAdminRootToDashboard()
    {
        CustomNavRoutes.TryNormalize("/admin", out var normalized).Should().BeTrue();
        normalized.Should().Be("/admin/dashboard");
    }

    [Test]
    public void TryNormalize_ShouldAcceptAdminDashboard()
    {
        CustomNavRoutes.TryNormalize("/admin/libraries", out var normalized).Should().BeTrue();
        normalized.Should().Be("/admin/libraries");
        CustomNavRoutes.Find("/admin/libraries")!.AdminOnly.Should().BeTrue();
    }

    [Test]
    public void TryNormalize_ShouldRejectUnknownAdminPath()
    {
        CustomNavRoutes.TryNormalize("/admin/streams", out _).Should().BeFalse();
    }

    [Test]
    public void TryNormalize_ShouldMapLegacyHomeSettingsPath()
    {
        CustomNavRoutes.TryNormalize("/settings/home", out var normalized).Should().BeTrue();
        normalized.Should().Be("/settings/home-layout");
    }

    [Test]
    public void Find_ShouldMarkDownloadsAsNativeOnly()
    {
        var route = CustomNavRoutes.Find("/my-space/downloads");
        route.Should().NotBeNull();
        route!.NativeOnly.Should().BeTrue();
        CustomNavRoutes.IsAvailableForClient(route, isNativeClient: false).Should().BeFalse();
        CustomNavRoutes.IsAvailableForClient(route, isNativeClient: true).Should().BeTrue();
    }

    [Test]
    public void FilterForClient_ShouldHideNativeOnlyAppRoutesOnWeb()
    {
        var items = new[]
        {
            new CustomNavItemDto
            {
                Id = Guid.NewGuid(),
                Kind = CustomNavItemKind.AppRoute,
                Route = "/search"
            },
            new CustomNavItemDto
            {
                Id = Guid.NewGuid(),
                Kind = CustomNavItemKind.AppRoute,
                Route = "/my-space/downloads"
            }
        };

        var filtered = CustomNavRoutes.FilterForClient(items, isNativeClient: false);
        filtered.Should().ContainSingle(i => i.Route == "/search");
        filtered.Should().NotContain(i => i.Route == "/my-space/downloads");
    }

    [Test]
    public void NormalizeBrowseQuery_ShouldDropUnknownKeys()
    {
        CustomNavRoutes.NormalizeBrowseQuery("?sort=title&foo=bar").Should().Be("sort=title");
    }
}
