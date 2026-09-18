using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Dtos.Entities;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class CustomNavStoreTests
{
    [Test]
    public async Task ReloadAsync_ShouldCacheLayoutAndGroups()
    {
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
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
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Kind = CustomNavItemKind.LibraryGroup,
                    LibraryGroupId = groupId
                }
            ]
        };

        using var sut = CreateStore(layout, [CreateGroup(groupId, "Films")]);

        await sut.EnsureLoadedAsync();

        sut.IsLoaded.Should().BeTrue();
        sut.Layout.Enabled.Should().BeTrue();
        sut.Layout.Items.Should().ContainSingle().Which.LibraryGroupId.Should().Be(groupId);
        sut.Groups.Should().ContainSingle().Which.Title.Should().Be("Films");
    }

    [Test]
    public async Task EnsureLoadedAsync_ShouldNotReload_WhenAlreadyLoaded()
    {
        var prefs = Substitute.For<IUserPreferencesService>();
        prefs.GetCustomNavLayoutAsync(Arg.Any<CancellationToken>())
            .Returns(CustomNavLayoutDto.Disabled());
        prefs.GetEffectiveGeneralPreferencesAsync(Arg.Any<CancellationToken>())
            .Returns(new GeneralPreferencesDto());

        using var sut = CreateStore(prefs: prefs);

        await sut.EnsureLoadedAsync();
        await sut.EnsureLoadedAsync();

        await prefs.Received(1).GetCustomNavLayoutAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Invalidate_ShouldClearCache_WhenCalledAfterLoad()
    {
        using var sut = CreateStore();

        await sut.EnsureLoadedAsync();
        sut.Invalidate();

        sut.IsLoaded.Should().BeFalse();
        sut.Layout.Enabled.Should().BeFalse();
        sut.Groups.Should().BeEmpty();
    }

    [Test]
    public void HubLayoutUpdated_ShouldApplyLayoutAndRaiseChanged()
    {
        var hub = Substitute.For<ICustomNavHubEvents>();
        using var sut = CreateStore(hubEvents: hub);
        var raised = 0;
        sut.Changed += () => raised++;

        var layout = new CustomNavLayoutDto
        {
            Enabled = true,
            Placement = CustomNavPlacement.Bar,
            BarScope = CustomNavBarScope.Everywhere,
            BarShowIcons = true,
            Items =
            [
                new CustomNavItemDto
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Kind = CustomNavItemKind.AppRoute,
                    Route = "/explore"
                }
            ]
        };

        hub.CustomNavLayoutUpdated += Raise.Event<Action<CustomNavLayoutDto>>(layout);

        sut.IsLoaded.Should().BeTrue();
        sut.Layout.Enabled.Should().BeTrue();
        sut.Layout.Placement.Should().Be(CustomNavPlacement.Bar);
        raised.Should().Be(1);
    }

    private static CustomNavStore CreateStore(
        CustomNavLayoutDto? layout = null,
        List<LibraryGroupDto>? groups = null,
        IUserPreferencesService? prefs = null,
        ICustomNavHubEvents? hubEvents = null)
    {
        prefs ??= Substitute.For<IUserPreferencesService>();
        prefs.GetCustomNavLayoutAsync(Arg.Any<CancellationToken>())
            .Returns(layout ?? CustomNavLayoutDto.Disabled());
        prefs.GetEffectiveGeneralPreferencesAsync(Arg.Any<CancellationToken>())
            .Returns(new GeneralPreferencesDto());

        var libraries = Substitute.For<ILibraryService>();
        libraries.GetLibraryGroupsAsync(Arg.Any<CancellationToken>())
            .Returns(groups ?? new List<LibraryGroupDto>());

        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IUserPreferencesService)).Returns(prefs);
        provider.GetService(typeof(ILibraryService)).Returns(libraries);
        provider.GetService(typeof(ICollectionService)).Returns(Substitute.For<ICollectionService>());
        provider.GetService(typeof(IPlaylistService)).Returns(Substitute.For<IPlaylistService>());

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return new CustomNavStore(scopeFactory, hubEvents ?? Substitute.For<ICustomNavHubEvents>());
    }

    private static LibraryGroupDto CreateGroup(Guid id, string title) => new()
    {
        Id = id,
        Title = title,
        MediaType = LibraryMediaType.Movie
    };
}
