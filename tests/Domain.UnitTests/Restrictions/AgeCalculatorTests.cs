using K7.Server.Domain.Restrictions;

namespace K7.Server.Domain.UnitTests.Restrictions;

[TestFixture]
public class AgeCalculatorTests
{
    [Test]
    public void GetCompletedYears_ShouldReturnAge_OnBirthday()
    {
        var birth = new DateOnly(2014, 9, 10);
        var today = new DateOnly(2026, 9, 10);

        AgeCalculator.GetCompletedYears(birth, today).Should().Be(12);
    }

    [Test]
    public void GetCompletedYears_ShouldReturnPreviousAge_TheDayBeforeBirthday()
    {
        var birth = new DateOnly(2014, 9, 10);
        var today = new DateOnly(2026, 9, 9);

        AgeCalculator.GetCompletedYears(birth, today).Should().Be(11);
    }

    [Test]
    public void GetCompletedYears_ShouldReturnZero_WhenBirthDateIsInTheFuture()
    {
        var birth = new DateOnly(2027, 1, 1);
        var today = new DateOnly(2026, 9, 10);

        AgeCalculator.GetCompletedYears(birth, today).Should().Be(0);
    }
}
