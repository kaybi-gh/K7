using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Models;

[TestFixture]
public class HomeRowEditModelTests
{
    private static readonly Guid LibraryA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LibraryB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GroupA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Test]
    public void ToDto_ShouldPreferLibraryGroupIds_AndClearLibraryIds()
    {
        var model = new HomeRowEditModel
        {
            Id = Guid.NewGuid(),
            Title = "Series",
            LibraryIds = [LibraryA, LibraryB],
            LibraryGroupIds = [GroupA],
            OrderBy = MediaOrderingOption.CreatedDesc,
            PageSize = 20,
            IsVisible = true
        };

        var dto = model.ToDto();

        dto.LibraryGroupIds.Should().Equal(GroupA);
        dto.LibraryIds.Should().BeNull();
    }

    [Test]
    public void ToDto_ShouldKeepLibraryIds_WhenNoLibraryGroups()
    {
        var model = new HomeRowEditModel
        {
            Id = Guid.NewGuid(),
            Title = "Subset",
            LibraryIds = [LibraryA],
            LibraryGroupIds = [],
            OrderBy = MediaOrderingOption.CreatedDesc,
            PageSize = 20,
            IsVisible = true
        };

        var dto = model.ToDto();

        dto.LibraryIds.Should().Equal(LibraryA);
        dto.LibraryGroupIds.Should().BeNull();
    }

    [Test]
    public void ToDto_ShouldClearCatalogScope_WhenContinueWatching()
    {
        var model = new HomeRowEditModel
        {
            Id = Guid.NewGuid(),
            Title = "ContinueWatching",
            ContinueWatching = true,
            LibraryIds = [LibraryA],
            LibraryGroupIds = [GroupA],
            MediaTypes = [MediaType.Movie],
            PageSize = 20,
            IsVisible = true
        };

        var dto = model.ToDto();

        dto.LibraryIds.Should().BeNull();
        dto.LibraryGroupIds.Should().BeNull();
        dto.MediaTypes.Should().BeNull();
        dto.OrderBy.Should().BeNull();
    }

    [Test]
    public void FromDto_ShouldRoundTripLibraryGroupIds()
    {
        var dto = new HomeRowConfigDto
        {
            Id = GroupA,
            Title = "NewlyAddedIn|Series",
            DisplayType = HomeRowDisplayType.Carousel,
            LibraryGroupIds = [GroupA],
            OrderBy = [MediaOrderingOption.CreatedDesc],
            PageSize = 50,
            ContinueWatching = false,
            IsVisible = true,
            Order = 2
        };

        var model = HomeRowEditModel.FromDto(dto);
        var roundTrip = model.ToDto();

        model.LibraryGroupIds.Should().Equal(GroupA);
        model.LibraryIds.Should().BeEmpty();
        roundTrip.LibraryGroupIds.Should().Equal(GroupA);
        roundTrip.LibraryIds.Should().BeNull();
    }
}
