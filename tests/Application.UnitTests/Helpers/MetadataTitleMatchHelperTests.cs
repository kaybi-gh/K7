using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MetadataTitleMatchHelperTests
{
    [Test]
    public void Score_ShouldPreferExactTitle_OverSimilarLongerTitle()
    {
        var exact = MetadataTitleMatchHelper.Score("Baki", "Baki", 2018, 2018);
        var similar = MetadataTitleMatchHelper.Score("Baki", "Meri Baji", 2018, 2018);

        exact.Should().BeGreaterThan(similar);
    }

    [Test]
    public void PickBest_ShouldSelectExactBaki_WhenMeriBajiIsListedFirst()
    {
        var candidates = new[]
        {
            new Candidate("Meri Baji", 2018),
            new Candidate("Baki", 2018),
            new Candidate("Baki the Grappler", 2001)
        };

        var best = MetadataTitleMatchHelper.PickBest(
            "Baki",
            2018,
            candidates,
            c => c.Title,
            c => c.Year);

        best.Should().NotBeNull();
        best!.Title.Should().Be("Baki");
    }

    [Test]
    public void OrderByBestMatch_ShouldPutExactMatchFirst()
    {
        var candidates = new[]
        {
            new Candidate("Meri Baji", 2018),
            new Candidate("Baki Hanma", 2021),
            new Candidate("Baki", 2018)
        };

        var ordered = MetadataTitleMatchHelper.OrderByBestMatch(
            "Baki",
            2018,
            candidates,
            c => c.Title,
            c => c.Year);

        ordered.Select(c => c.Title).Should().ContainInOrder("Baki", "Baki Hanma", "Meri Baji");
    }

    [Test]
    public void Score_ShouldUseAlternateTitle_WhenPrimaryDiffers()
    {
        var score = MetadataTitleMatchHelper.Score(
            "Baki",
            2018,
            primaryTitle: "Something Else",
            resultYear: 2018,
            "BAKI");

        score.Should().Be(MetadataTitleMatchHelper.Score("Baki", "BAKI", 2018, 2018));
    }

    [Test]
    public void Score_ShouldPenalizeYearMismatch()
    {
        var matchingYear = MetadataTitleMatchHelper.Score("Baki", "Baki", 2018, 2018);
        var mismatchingYear = MetadataTitleMatchHelper.Score("Baki", "Baki", 2018, 2001);

        matchingYear.Should().BeGreaterThan(mismatchingYear);
    }

    [Test]
    public void OrderByBestMatch_ShouldPreferExactDragons_OverDragonsOrDinosaurs()
    {
        var candidates = new[]
        {
            new Candidate("Dragons or Dinosaurs: Creation or Evolution", 2010, Popularity: 0.04),
            new Candidate("Dragons", 2010, Popularity: 19.18),
            new Candidate("How to Train Your Dragon", 2010, Popularity: 19.18)
        };

        var ordered = MetadataTitleMatchHelper.OrderByBestMatch(
            "Dragons",
            2010,
            candidates,
            c => c.Title,
            c => c.Year,
            popularitySelector: c => c.Popularity);

        ordered[0].Title.Should().Be("Dragons");
        ordered[0].Title.Should().NotBe("Dragons or Dinosaurs: Creation or Evolution");
    }

    [Test]
    public void PickBest_EnglishTitles_ShouldPreferPopularHttyd_OverDragonsOrDinosaurs()
    {
        var candidates = new[]
        {
            new Candidate("Dragons or Dinosaurs: Creation or Evolution", 2010, Popularity: 0.04),
            new Candidate("How to Train Your Dragon", 2010, Popularity: 19.18),
            new Candidate("Dragon Hunters", 2008, Popularity: 2.68),
            new Candidate("Merlin and the War of the Dragons", 2008, Popularity: 1.0)
        };

        var best = MetadataTitleMatchHelper.PickBest(
            "Dragons",
            2010,
            candidates,
            c => c.Title,
            c => c.Year,
            popularitySelector: c => c.Popularity);

        best.Should().NotBeNull();
        best!.Title.Should().Be("How to Train Your Dragon");
    }

    private sealed record Candidate(string Title, int Year, double? Popularity = null);
}
