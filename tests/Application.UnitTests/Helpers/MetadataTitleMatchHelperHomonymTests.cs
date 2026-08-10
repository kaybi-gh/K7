using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MetadataTitleMatchHelperHomonymTests
{
    private sealed record Candidate(string Title, int? Year, double? Popularity);

    [Test]
    public void PickBest_ShouldPreferMatchingYear_WhenHomonymsShareExactTitle()
    {
        var candidates = new[]
        {
            new Candidate("The Buccaneers", 1956, 2),
            new Candidate("The Buccaneers", 2023, 40)
        };

        var best = MetadataTitleMatchHelper.PickBest(
            "The Buccaneers",
            2023,
            candidates,
            c => c.Title,
            c => c.Year,
            popularitySelector: c => c.Popularity);

        best!.Year.Should().Be(2023);
    }

    [Test]
    public void PickBest_ShouldPreferHigherPopularity_WhenYearsAbsent()
    {
        var candidates = new[]
        {
            new Candidate("Bull", 2000, 1),
            new Candidate("Bull", 2016, 55)
        };

        var best = MetadataTitleMatchHelper.PickBest(
            "Bull",
            queryYear: null,
            candidates,
            c => c.Title,
            c => c.Year,
            popularitySelector: c => c.Popularity);

        best!.Year.Should().Be(2016);
    }
}
