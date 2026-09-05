using AwesomeAssertions;
using K7.Server.Application.Features.OpenSubsonic;

namespace K7.Server.Application.UnitTests.Features.OpenSubsonic;

public class OpenSubsonicConstantsTests
{
    [Test]
    public void StarredThreshold_ShouldTreatRatingsAboveFiveAsStarred()
    {
        OpenSubsonicConstants.StarredThreshold.Should().Be(5);
        (6 > OpenSubsonicConstants.StarredThreshold).Should().BeTrue();
        (5 > OpenSubsonicConstants.StarredThreshold).Should().BeFalse();
    }

    [Test]
    public void RatingScaleFactor_ShouldMapOpenSubsonicFiveToTen()
    {
        OpenSubsonicConstants.RatingScaleFactor.Should().Be(2);
        (5 * OpenSubsonicConstants.RatingScaleFactor).Should().Be(10);
        (3 * OpenSubsonicConstants.RatingScaleFactor).Should().Be(6);
    }
}
