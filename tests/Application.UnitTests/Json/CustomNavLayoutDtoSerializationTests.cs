using System.Text.Json;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;
using K7.Shared.Json;

namespace K7.Server.Application.UnitTests.Json;

public class CustomNavLayoutDtoSerializationTests
{
    private static readonly JsonSerializerOptions Options = K7JsonSerializerOptions.CreateDefault();

    [Test]
    public void RoundTrip_ShouldPreserveItemsAndEnums()
    {
        var groupId = Guid.NewGuid();
        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.Bar,
            BarScope = CustomNavBarScope.Everywhere,
            BarShowIcons = false,
            Items =
            [
                new CustomNavItemDto
                {
                    Id = Guid.NewGuid(),
                    Kind = CustomNavItemKind.LibraryGroup,
                    LibraryGroupId = groupId,
                    TapAction = ExploreTapAction.Browse,
                    Title = "Films"
                }
            ]
        };

        var json = JsonSerializer.Serialize(layout, Options);
        var deserialized = JsonSerializer.Deserialize<CustomNavLayoutDto>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.Enabled.Should().BeTrue();
        deserialized.Placement.Should().Be(CustomNavPlacement.Bar);
        deserialized.BarScope.Should().Be(CustomNavBarScope.Everywhere);
        deserialized.ShowBarOnHome.Should().BeTrue();
        deserialized.ShowBarOnExplore.Should().BeTrue();
        deserialized.FeedRowTitle.Should().BeNull();
        deserialized.Items.Should().ContainSingle();
        deserialized.Items[0].LibraryGroupId.Should().Be(groupId);
        deserialized.Items[0].TapAction.Should().Be(ExploreTapAction.Browse);
        deserialized.Items[0].CardColor.Should().BeNull();
        deserialized.Items[0].CoverPictureId.Should().BeNull();
    }

    [Test]
    public void Serialize_ShouldUseCamelCaseAndStringEnums()
    {
        var layout = CustomNavLayoutDto.Disabled() with
        {
            Enabled = true,
            Placement = CustomNavPlacement.FeedRow,
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

        var json = JsonSerializer.Serialize(layout, Options);

        json.Should().Contain("\"placement\":\"FeedRow\"");
        json.Should().Contain("\"kind\":\"AppRoute\"");
    }

    [Test]
    public void RoundTrip_ShouldPreserveFeedRowTitle()
    {
        var layout = CustomNavLayoutDto.Disabled() with
        {
            Enabled = true,
            FeedRowTitle = "Bibliotheques",
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

        var json = JsonSerializer.Serialize(layout, Options);
        var deserialized = JsonSerializer.Deserialize<CustomNavLayoutDto>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.FeedRowTitle.Should().Be("Bibliotheques");
    }

    [Test]
    public void RoundTrip_ShouldPreserveRowPages()
    {
        var layout = CustomNavLayoutDto.Disabled() with
        {
            Enabled = true,
            ShowRowOnHome = false,
            ShowRowOnExploreFeeds = true,
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

        var json = JsonSerializer.Serialize(layout, Options);
        var deserialized = JsonSerializer.Deserialize<CustomNavLayoutDto>(json, Options);

        json.Should().Contain("\"showRowOnHome\":false");
        json.Should().Contain("\"showRowOnExploreFeeds\":true");
        json.Should().NotContain("feedRowScope");
        deserialized.Should().NotBeNull();
        deserialized!.ShowRowOnHome.Should().BeFalse();
        deserialized.ShowRowOnExploreFeeds.Should().BeTrue();
        deserialized.FeedRowScope.Should().BeNull();
    }

    [Test]
    public void Deserialize_ShouldReadLegacyFeedRowScope()
    {
        var json = """
            {"enabled":true,"placement":"FeedRow","barScope":"HomeExplore","barShowIcons":true,"feedRowScope":"HomeAndGroupFeeds","items":[]}
            """;

        var deserialized = JsonSerializer.Deserialize<CustomNavLayoutDto>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.FeedRowScope.Should().Be(CustomNavFeedRowScope.HomeAndGroupFeeds);
        deserialized.ShowRowOnHome.Should().BeTrue();
        deserialized.ShowRowOnExploreFeeds.Should().BeFalse();
    }

    [Test]
    public void Deserialize_ShouldDefaultDevices_WhenMissing()
    {
        var json = """
            {"enabled":true,"placement":"FeedRow","barScope":"HomeExplore","barShowIcons":true,"items":[]}
            """;

        var deserialized = JsonSerializer.Deserialize<CustomNavLayoutDto>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.ShowOnDesktop.Should().BeTrue();
        deserialized.ShowOnPhone.Should().BeFalse();
        deserialized.ShowOnTv.Should().BeTrue();
    }

    [Test]
    public void RoundTrip_ShouldPreserveDevices()
    {
        var layout = CustomNavLayoutDto.Disabled() with
        {
            Enabled = true,
            ShowOnDesktop = false,
            ShowOnPhone = true,
            ShowOnTv = true,
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

        var json = JsonSerializer.Serialize(layout, Options);
        var deserialized = JsonSerializer.Deserialize<CustomNavLayoutDto>(json, Options);

        json.Should().Contain("\"showOnDesktop\":false");
        json.Should().Contain("\"showOnPhone\":true");
        deserialized.Should().NotBeNull();
        deserialized!.ShowOnDesktop.Should().BeFalse();
        deserialized.ShowOnPhone.Should().BeTrue();
        deserialized.ShowOnTv.Should().BeTrue();
    }

    [Test]
    public void RoundTrip_ShouldPreserveBarPages()
    {
        var layout = CustomNavLayoutDto.Disabled() with
        {
            Enabled = true,
            Placement = CustomNavPlacement.Bar,
            ShowBarOnHome = false,
            ShowBarOnExplore = true,
            ShowBarOnMySpace = true,
            ShowBarOnSettings = false,
            ShowBarOnMedia = true,
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

        var json = JsonSerializer.Serialize(layout, Options);
        var deserialized = JsonSerializer.Deserialize<CustomNavLayoutDto>(json, Options);

        json.Should().Contain("\"showBarOnMySpace\":true");
        json.Should().Contain("\"showBarOnMedia\":true");
        json.Should().NotContain("barScope");
        deserialized.Should().NotBeNull();
        deserialized!.ShowBarOnHome.Should().BeFalse();
        deserialized.ShowBarOnExplore.Should().BeTrue();
        deserialized.ShowBarOnMySpace.Should().BeTrue();
        deserialized.ShowBarOnSettings.Should().BeFalse();
        deserialized.ShowBarOnMedia.Should().BeTrue();
    }
}
