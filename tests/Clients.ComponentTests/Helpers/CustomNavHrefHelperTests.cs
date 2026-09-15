using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Dtos.Entities;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Helpers;

public class CustomNavHrefHelperTests
{
    [Test]
    public void GetHref_ShouldUseBrowseQuery_ForLibraryBrowse()
    {
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var item = new CustomNavItemDto
        {
            Id = Guid.NewGuid(),
            Kind = CustomNavItemKind.LibraryBrowse,
            LibraryGroupId = groupId,
            BrowseQuery = "sort=title"
        };

        CustomNavHrefHelper.GetHref(item, [], null)
            .Should().Be($"/library-groups/{groupId}?sort=title");
    }

    [Test]
    public void GetHref_ShouldHonorExplicitTapAction()
    {
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var item = new CustomNavItemDto
        {
            Id = Guid.NewGuid(),
            Kind = CustomNavItemKind.LibraryGroup,
            LibraryGroupId = groupId,
            TapAction = ExploreTapAction.Browse
        };

        CustomNavHrefHelper.GetHref(item, [], new GeneralPreferencesDto())
            .Should().Be($"/library-groups/{groupId}");
    }

    [Test]
    public void GetTitle_ShouldPreferOverride_ThenGroupTitle()
    {
        var groupId = Guid.NewGuid();
        var groups = new List<LibraryGroupDto>
        {
            new()
            {
                Id = groupId,
                Title = "Films",
                MediaType = LibraryMediaType.Movie
            }
        };
        var item = new CustomNavItemDto
        {
            Id = Guid.NewGuid(),
            Kind = CustomNavItemKind.LibraryGroup,
            LibraryGroupId = groupId,
            Title = "Cinema"
        };

        CustomNavHrefHelper.GetTitle(item, groups, null).Should().Be("Cinema");
        CustomNavHrefHelper.GetTitle(item with { Title = null }, groups, null).Should().Be("Films");
    }

    [Test]
    public void GetCardColor_ShouldPreferOverride_ThenGroupColor()
    {
        var groupId = Guid.NewGuid();
        var groups = new List<LibraryGroupDto>
        {
            new()
            {
                Id = groupId,
                Title = "Films",
                MediaType = LibraryMediaType.Movie,
                CardColor = "#781e1e"
            }
        };
        var item = new CustomNavItemDto
        {
            Id = Guid.NewGuid(),
            Kind = CustomNavItemKind.LibraryGroup,
            LibraryGroupId = groupId,
            CardColor = "#143c78"
        };

        CustomNavHrefHelper.GetCardColor(item, groups).Should().Be("#143c78");
        CustomNavHrefHelper.GetCardColor(item with { CardColor = null }, groups).Should().Be("#781e1e");
    }

    [Test]
    public void GetCoverPictureId_ShouldPreferOverride_ThenGroupCover()
    {
        var groupId = Guid.NewGuid();
        var groupCover = Guid.NewGuid();
        var itemCover = Guid.NewGuid();
        var groups = new List<LibraryGroupDto>
        {
            new()
            {
                Id = groupId,
                Title = "Films",
                MediaType = LibraryMediaType.Movie,
                CoverPictureId = groupCover
            }
        };
        var item = new CustomNavItemDto
        {
            Id = Guid.NewGuid(),
            Kind = CustomNavItemKind.LibraryGroup,
            LibraryGroupId = groupId,
            CoverPictureId = itemCover
        };

        CustomNavHrefHelper.GetCoverPictureId(item, groups).Should().Be(itemCover);
        CustomNavHrefHelper.GetCoverPictureId(item with { CoverPictureId = null }, groups).Should().Be(groupCover);
    }

    [Test]
    public void GetHref_ShouldUseCollectionAndPlaylistTargets()
    {
        var collectionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var playlistId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var dynamicId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        CustomNavHrefHelper.GetHref(new CustomNavItemDto
        {
            Id = Guid.NewGuid(),
            Kind = CustomNavItemKind.Collection,
            TargetId = collectionId
        }, [], null).Should().Be($"/collections/{collectionId}");

        CustomNavHrefHelper.GetHref(new CustomNavItemDto
        {
            Id = Guid.NewGuid(),
            Kind = CustomNavItemKind.Playlist,
            TargetId = playlistId
        }, [], null).Should().Be($"/playlists/{playlistId}");

        CustomNavHrefHelper.GetHref(new CustomNavItemDto
        {
            Id = Guid.NewGuid(),
            Kind = CustomNavItemKind.DynamicPlaylist,
            TargetId = dynamicId
        }, [], null).Should().Be($"/dynamic-playlists/{dynamicId}");
    }

    [Test]
    public void ToMenuIcon_ShouldStripPhosphorPrefix()
    {
        CustomNavHrefHelper.ToMenuIcon("ph ph-film-strip").Should().Be("film-strip");
        CustomNavHrefHelper.ToMenuIcon("ph-magnifying-glass").Should().Be("magnifying-glass");
        CustomNavHrefHelper.ToMenuIcon("rows").Should().Be("rows");
        CustomNavHrefHelper.ToMenuIcon(null).Should().BeEmpty();
        CustomNavHrefHelper.ToMenuIcon("  ").Should().BeEmpty();
    }
}
