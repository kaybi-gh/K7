using K7.Server.Application.Features.Home.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.Home.Services;

public class HomeLayoutSanitizerTests
{
    private static readonly Guid ContinueWatchingId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid LibraryA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LibraryB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LibraryC = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Test]
    public void Sanitize_ShouldRemoveRow_WhenAllReferencedLibrariesAreMissing()
    {
        var layout = new HomeLayoutDto
        {
            Rows =
            [
                CreateRow(ContinueWatchingId, "ContinueWatching", continueWatching: true, order: 0),
                CreateRow(Guid.NewGuid(), "Anime", libraryIds: [LibraryA], order: 1)
            ]
        };

        var result = HomeLayoutSanitizer.Sanitize(layout, new HashSet<Guid> { LibraryB });

        result.Rows.Should().ContainSingle();
        result.Rows[0].Title.Should().Be("ContinueWatching");
    }

    [Test]
    public void Sanitize_ShouldKeepRowWithRemainingLibraries_WhenOnlySomeLibrariesAreMissing()
    {
        var layout = new HomeLayoutDto
        {
            Rows =
            [
                CreateRow(Guid.NewGuid(), "Mixed", libraryIds: [LibraryA, LibraryB], order: 0)
            ]
        };

        var result = HomeLayoutSanitizer.Sanitize(layout, new HashSet<Guid> { LibraryB, LibraryC });

        result.Rows.Should().ContainSingle();
        result.Rows[0].LibraryIds.Should().Equal(LibraryB);
    }

    [Test]
    public void Sanitize_ShouldKeepRowsWithoutLibraryFilter()
    {
        var layout = new HomeLayoutDto
        {
            Rows =
            [
                CreateRow(Guid.NewGuid(), "AllMovies", mediaTypes: [MediaType.Movie], order: 0)
            ]
        };

        var result = HomeLayoutSanitizer.Sanitize(layout, []);

        result.Rows.Should().ContainSingle();
        result.Rows[0].Title.Should().Be("AllMovies");
    }

    [Test]
    public void Sanitize_ShouldRenumberRemainingRows()
    {
        var layout = new HomeLayoutDto
        {
            Rows =
            [
                CreateRow(ContinueWatchingId, "ContinueWatching", continueWatching: true, order: 0),
                CreateRow(Guid.NewGuid(), "Anime", libraryIds: [LibraryA], order: 1),
                CreateRow(Guid.NewGuid(), "Movies", libraryIds: [LibraryB], order: 2)
            ]
        };

        var result = HomeLayoutSanitizer.Sanitize(layout, new HashSet<Guid> { LibraryB });

        result.Rows.Should().HaveCount(2);
        result.Rows[0].Order.Should().Be(0);
        result.Rows[1].Order.Should().Be(1);
        result.Rows[1].Title.Should().Be("Movies");
    }

    private static HomeRowConfigDto CreateRow(
        Guid id,
        string title,
        bool continueWatching = false,
        IReadOnlyList<Guid>? libraryIds = null,
        IReadOnlyList<MediaType>? mediaTypes = null,
        int order = 0) =>
        new()
        {
            Id = id,
            Title = title,
            DisplayType = HomeRowDisplayType.Carousel,
            ContinueWatching = continueWatching,
            LibraryIds = libraryIds,
            MediaTypes = mediaTypes,
            OrderBy = [MediaOrderingOption.CreatedDesc],
            PageSize = 20,
            IsVisible = true,
            Order = order
        };
}
