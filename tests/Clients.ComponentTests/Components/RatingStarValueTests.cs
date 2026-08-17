using K7.Clients.Shared.UI.Components;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class RatingStarValueTests
{
    [TestCase(0, 0)]
    [TestCase(0.04, 0)]
    [TestCase(0.1, 1)]
    [TestCase(0.5, 5)]
    [TestCase(0.7, 7)]
    [TestCase(1, 10)]
    public void FromRatio_ShouldMapToTenPointScale(double ratio, int expected) =>
        RatingStarValue.FromRatio(ratio).Should().Be(expected);

    [Test]
    public void StarModifierClass_ShouldUseHalfForOddValues()
    {
        RatingStarValue.StarModifierClass(1, 7).Should().Be("star--filled");
        RatingStarValue.StarModifierClass(2, 7).Should().Be("star--filled");
        RatingStarValue.StarModifierClass(3, 7).Should().Be("star--filled");
        RatingStarValue.StarModifierClass(4, 7).Should().Be("star--half");
        RatingStarValue.StarModifierClass(5, 7).Should().BeEmpty();
    }

    [Test]
    public void FormatStarsLabel_ShouldShowHalfSteps()
    {
        RatingStarValue.FormatStarsLabel(0).Should().Be("0/5");
        RatingStarValue.FormatStarsLabel(7).Should().Be("3.5/5");
        RatingStarValue.FormatStarsLabel(10).Should().Be("5/5");
    }
}
