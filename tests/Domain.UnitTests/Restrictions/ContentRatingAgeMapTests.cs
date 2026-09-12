using K7.Server.Domain.Restrictions;

namespace K7.Server.Domain.UnitTests.Restrictions;

[TestFixture]
public class ContentRatingAgeMapTests
{
    [Test]
    public void TryGetMinimumAge_ShouldMapCommonOfficialRatings()
    {
        ContentRatingAgeMap.TryGetMinimumAge("12").Should().Be(12);
        ContentRatingAgeMap.TryGetMinimumAge("pg-13").Should().Be(13);
        ContentRatingAgeMap.TryGetMinimumAge("r").Should().Be(17);
        ContentRatingAgeMap.TryGetMinimumAge("tp").Should().Be(0);
        ContentRatingAgeMap.TryGetMinimumAge("unknown").Should().BeNull();
    }

    [Test]
    public void GetAllowedNormalizedKeys_ShouldExcludeRatingsAboveViewerAge()
    {
        var allowed = ContentRatingAgeMap.GetAllowedNormalizedKeys(11);

        allowed.Should().Contain("g");
        allowed.Should().Contain("10");
        allowed.Should().NotContain("12");
        allowed.Should().NotContain("pg-13");
        allowed.Should().NotContain("18");
    }

    [Test]
    public void GetAllowedNormalizedKeys_ShouldAllowAdultRatings_WhenViewerIs18()
    {
        var allowed = ContentRatingAgeMap.GetAllowedNormalizedKeys(18);

        allowed.Should().Contain("18");
        allowed.Should().Contain("nc-17");
        allowed.Should().Contain("pg-13");
    }
}
