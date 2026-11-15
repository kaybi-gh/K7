using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using K7.Shared.Home;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class HomeRowPresetsTests
{
    private static readonly LibraryGroupDto _movies = new()
    {
        Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Title = "Movies",
        MediaType = default
    };

    private static readonly LibraryGroupDto _series = new()
    {
        Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        Title = "Series",
        MediaType = default
    };

    [Test]
    public void Available_ShouldOfferTheSmartRowsAndOneRowPerLibraryGroup_OnAnEmptyHome()
    {
        var presets = HomeRowPresets.Available([], [_series, _movies]);

        presets.Select(p => p.Title).Should().Equal(
            HomeLayoutRowTitles.ContinueWatching,
            HomeLayoutRowTitles.RecommendedForYou,
            HomeLayoutRowTitles.NewlyAddedIn("Movies"),
            HomeLayoutRowTitles.NewlyAddedIn("Series"));
    }

    [Test]
    public void Available_ShouldBuildRowsTheFeedUnderstands()
    {
        var presets = HomeRowPresets.Available([], [_movies]);

        var resume = presets[0];
        resume.ContinueWatching.Should().BeTrue();
        resume.ToDto().OrderBy.Should().BeNull();

        var recommended = presets[1];
        recommended.ToDto().OrderBy.Should().Equal(MediaOrderingOption.RecommendedForYou);

        var newlyAdded = presets[2];
        newlyAdded.Id.Should().Be(_movies.Id);
        newlyAdded.ToDto().LibraryGroupIds.Should().Equal(_movies.Id);
        newlyAdded.ToDto().OrderBy.Should().Equal(MediaOrderingOption.CreatedDesc);
    }

    [Test]
    public void Available_ShouldLeaveOutPresetsAlreadyOnTheHome()
    {
        var rows = new List<HomeRowEditModel>
        {
            new() { Id = Guid.NewGuid(), Title = HomeLayoutRowTitles.ContinueWatching, ContinueWatching = true },
            new() { Id = _movies.Id, Title = HomeLayoutRowTitles.NewlyAddedIn("Movies") }
        };

        var presets = HomeRowPresets.Available(rows, [_movies, _series]);

        presets.Select(p => p.Title).Should().Equal(
            HomeLayoutRowTitles.RecommendedForYou,
            HomeLayoutRowTitles.NewlyAddedIn("Series"));
    }

    [Test]
    public void Available_ShouldGiveANewId_WhenAnotherRowAlreadyUsesTheOneOfThePreset()
    {
        var rows = new List<HomeRowEditModel> { new() { Id = _movies.Id, Title = "My own row" } };

        var preset = HomeRowPresets.Available(rows, [_movies])
            .Single(p => p.Title == HomeLayoutRowTitles.NewlyAddedIn("Movies"));

        preset.Id.Should().NotBe(_movies.Id);
        preset.LibraryGroupIds.Should().Equal(_movies.Id);
    }

    [Test]
    public void All_ShouldKeepPresetsAlreadyOnTheHome_MarkedAsSuch()
    {
        var rows = new List<HomeRowEditModel>
        {
            new() { Id = Guid.NewGuid(), Title = HomeLayoutRowTitles.ContinueWatching, ContinueWatching = true }
        };

        var presets = HomeRowPresets.All(rows, [_movies]);

        presets.Select(p => (p.Row.Title, p.OnHome)).Should().Equal(
            (HomeLayoutRowTitles.ContinueWatching, true),
            (HomeLayoutRowTitles.RecommendedForYou, false),
            (HomeLayoutRowTitles.NewlyAddedIn("Movies"), false));
    }
}
