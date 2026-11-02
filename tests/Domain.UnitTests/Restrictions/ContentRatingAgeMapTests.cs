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
    public void TryGetMinimumAge_ShouldMapBareNumericAgeLabels()
    {
        ContentRatingAgeMap.TryGetMinimumAge("13").Should().Be(13);
        ContentRatingAgeMap.TryGetMinimumAge("1").Should().Be(1);
        ContentRatingAgeMap.TryGetMinimumAge("05").Should().Be(5);
        ContentRatingAgeMap.TryGetMinimumAge("21").Should().Be(21);
        ContentRatingAgeMap.TryGetMinimumAge("22").Should().BeNull();
        ContentRatingAgeMap.TryGetMinimumAge("nr").Should().BeNull();
    }

    [Test]
    public void GetAllowedNormalizedKeys_ShouldExcludeRatingsAboveViewerAge()
    {
        var allowed = ContentRatingAgeMap.GetAllowedNormalizedKeys(11);

        allowed.Should().Contain("g");
        allowed.Should().Contain("10");
        allowed.Should().Contain("11");
        allowed.Should().Contain("05");
        allowed.Should().NotContain("12");
        allowed.Should().NotContain("13");
        allowed.Should().NotContain("pg-13");
        allowed.Should().NotContain("18");
    }

    [Test]
    public void GetAllowedNormalizedKeys_ShouldAllowAdultRatings_WhenViewerIs18()
    {
        var allowed = ContentRatingAgeMap.GetAllowedNormalizedKeys(18);

        allowed.Should().Contain("18");
        allowed.Should().Contain("13");
        allowed.Should().Contain("nc-17");
        allowed.Should().Contain("pg-13");
    }
}
