using K7.Server.Domain.Enums;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.CustomNav;

public class CustomNavSanitizerTests
{
    private static readonly Guid GroupA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GroupB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public void Sanitize_ShouldDropLibraryItems_WhenGroupIsMissing()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            Items =
            [
                LibraryGroupItem(GroupA),
                LibraryGroupItem(GroupB)
            ]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.Items.Should().ContainSingle();
        result.Items[0].LibraryGroupId.Should().Be(GroupA);
        result.Enabled.Should().BeTrue();
    }

    [Test]
    public void Sanitize_ShouldDisable_WhenNoItemsRemain()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.Bar,
            BarScope = CustomNavBarScope.Everywhere,
            BarShowIcons = false,
            Items = [LibraryGroupItem(GroupA)]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid>());

        result.Enabled.Should().BeFalse();
        result.Items.Should().BeEmpty();
    }

    [Test]
    public void Sanitize_ShouldKeepAllowlistedAppRoute_AndDropUnknownRoute()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.Bar,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            Items =
            [
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.AppRoute,
                    Route = "/my-space/playlists"
                },
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.AppRoute,
                    Route = "https://evil.example"
                }
            ]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid>());

        result.Items.Should().ContainSingle();
        result.Items[0].Route.Should().Be("/my-space/playlists");
    }

    [Test]
    public void Sanitize_ShouldKeepBrowseQueryKeys_AndDropUnknownKeys()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            Items =
            [
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.LibraryBrowse,
                    LibraryGroupId = GroupA,
                    BrowseQuery = "sort=title&inject=1&filter=abc"
                }
            ]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.Items.Should().ContainSingle();
        result.Items[0].BrowseQuery.Should().Be("sort=title&filter=abc");
    }

    [Test]
    public void Sanitize_ShouldKeepCardAppearance()
    {
        var coverId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            Items =
            [
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.LibraryGroup,
                    LibraryGroupId = GroupA,
                    CardColor = "781e1e",
                    CoverPictureId = coverId,
                    Icon = "film-strip"
                }
            ]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.Items.Should().ContainSingle();
        result.Items[0].CardColor.Should().Be("#781e1e");
        result.Items[0].CoverPictureId.Should().Be(coverId);
        result.Items[0].Icon.Should().Be("film-strip");
    }

    [Test]
    public void Sanitize_ShouldDropInvalidCardColor()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            Items =
            [
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.LibraryGroup,
                    LibraryGroupId = GroupA,
                    CardColor = "not-a-color"
                }
            ]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.Items[0].CardColor.Should().BeNull();
    }

    [Test]
    public void Sanitize_ShouldCapItemCount()
    {
        var items = Enumerable.Range(0, CustomNavLimits.MaxItems + 3)
            .Select(_ => LibraryGroupItem(GroupA))
            .ToList();

        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            Items = items
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.Items.Should().HaveCount(CustomNavLimits.MaxItems);
    }

    [Test]
    public void Sanitize_ShouldNormalizeFeedRowTitle()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            FeedRowTitle = "  Mes groupes  ",
            Items = [LibraryGroupItem(GroupA)]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.FeedRowTitle.Should().Be("Mes groupes");
    }

    [Test]
    public void Sanitize_ShouldDropBlankFeedRowTitle()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            FeedRowTitle = "   ",
            Items = [LibraryGroupItem(GroupA)]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.FeedRowTitle.Should().BeNull();
    }

    [Test]
    public void Sanitize_ShouldMigrateHomeAndGroupFeedsScope()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            FeedRowScope = CustomNavFeedRowScope.HomeAndGroupFeeds,
            Items = [LibraryGroupItem(GroupA)]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.ShowRowOnHome.Should().BeTrue();
        result.ShowRowOnExploreFeeds.Should().BeTrue();
        result.FeedRowScope.Should().BeNull();
    }

    [Test]
    public void Sanitize_ShouldFallbackRowPages_WhenNoneSelected()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            ShowRowOnHome = false,
            ShowRowOnExploreFeeds = false,
            Items = [LibraryGroupItem(GroupA)]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.ShowRowOnHome.Should().BeTrue();
        result.ShowRowOnExploreFeeds.Should().BeFalse();
    }

    [Test]
    public void Sanitize_ShouldMigrateEverywhereBarScope()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.Bar,
            BarScope = CustomNavBarScope.Everywhere,
            BarShowIcons = true,
            Items = [LibraryGroupItem(GroupA)]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.ShowBarOnHome.Should().BeTrue();
        result.ShowBarOnExplore.Should().BeTrue();
        result.ShowBarOnMySpace.Should().BeTrue();
        result.ShowBarOnSettings.Should().BeTrue();
        result.ShowBarOnMedia.Should().BeTrue();
        result.BarScope.Should().BeNull();
    }

    [Test]
    public void Sanitize_ShouldFallbackBarPages_WhenNoneSelected()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.Bar,
            BarShowIcons = true,
            ShowBarOnHome = false,
            ShowBarOnExplore = false,
            Items = [LibraryGroupItem(GroupA)]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.ShowBarOnHome.Should().BeTrue();
        result.ShowBarOnExplore.Should().BeTrue();
    }

    [Test]
    public void Sanitize_ShouldFallbackDevices_WhenNoneSelected()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarScope = CustomNavBarScope.HomeExplore,
            BarShowIcons = true,
            ShowOnDesktop = false,
            ShowOnPhone = false,
            ShowOnTv = false,
            Items = [LibraryGroupItem(GroupA)]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.ShowOnDesktop.Should().BeTrue();
        result.ShowOnPhone.Should().BeFalse();
        result.ShowOnTv.Should().BeTrue();
    }

    [Test]
    public void Sanitize_ShouldClearPhone_WhenPlacementIsBar()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.Bar,
            BarShowIcons = true,
            ShowOnDesktop = true,
            ShowOnPhone = true,
            ShowOnTv = true,
            Items = [LibraryGroupItem(GroupA)]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid> { GroupA });

        result.ShowOnPhone.Should().BeFalse();
        result.ShowOnDesktop.Should().BeTrue();
        result.ShowOnTv.Should().BeTrue();
    }

    [Test]
    public void Sanitize_ShouldKeepCollectionAndPlaylist_WhenTargetsExist()
    {
        var collectionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var playlistId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarShowIcons = true,
            Items =
            [
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.Collection,
                    TargetId = collectionId
                },
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.DynamicPlaylist,
                    TargetId = playlistId
                },
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.Collection,
                    TargetId = Guid.NewGuid()
                }
            ]
        };

        var result = CustomNavSanitizer.Sanitize(
            layout,
            new HashSet<Guid>(),
            new HashSet<Guid> { collectionId },
            new HashSet<Guid> { playlistId });

        result.Items.Should().HaveCount(2);
        result.Items[0].Kind.Should().Be(CustomNavItemKind.Collection);
        result.Items[0].TargetId.Should().Be(collectionId);
        result.Items[1].Kind.Should().Be(CustomNavItemKind.DynamicPlaylist);
        result.Items[1].TargetId.Should().Be(playlistId);
    }

    [Test]
    public void Sanitize_ShouldDropAdminAppRoute_WhenNotAllowed()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarShowIcons = true,
            Items =
            [
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.AppRoute,
                    Route = "/admin/dashboard"
                },
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.AppRoute,
                    Route = "/search"
                }
            ]
        };

        var result = CustomNavSanitizer.Sanitize(layout, new HashSet<Guid>());

        result.Items.Should().ContainSingle();
        result.Items[0].Route.Should().Be("/search");
    }

    [Test]
    public void Sanitize_ShouldKeepAdminAppRoute_WhenAllowed()
    {
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
            BarShowIcons = true,
            Items =
            [
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.AppRoute,
                    Route = "/admin"
                }
            ]
        };

        var result = CustomNavSanitizer.Sanitize(
            layout,
            new HashSet<Guid>(),
            null,
            null,
            allowAdminRoutes: true);

        result.Items.Should().ContainSingle();
        result.Items[0].Kind.Should().Be(CustomNavItemKind.AdminRoute);
        result.Items[0].Route.Should().Be("/admin/dashboard");
    }

    private static CustomNavItemDto LibraryGroupItem(Guid groupId) => new()
    {
        Id = Guid.NewGuid(),
        Kind = CustomNavItemKind.LibraryGroup,
        LibraryGroupId = groupId,
        TapAction = ExploreTapAction.Browse
    };
}
