using K7.Server.Application.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class IntroOutroSegmentCompletenessTests
{
    [Test]
    public void GetMissingTypeNames_ShouldReturnBoth_WhenNoSegments()
    {
        var missing = IntroOutroSegmentCompleteness.GetMissingTypeNames([]);

        missing.Should().Equal(
            IntroOutroSegmentCompleteness.IntroTypeName,
            IntroOutroSegmentCompleteness.OutroTypeName);
    }

    [Test]
    public void GetMissingTypeNames_ShouldReturnIntroOnly_WhenOutroPresent()
    {
        var missing = IntroOutroSegmentCompleteness.GetMissingTypeNames([MediaSegmentType.Outro]);

        missing.Should().ContainSingle()
            .Which.Should().Be(IntroOutroSegmentCompleteness.IntroTypeName);
    }

    [Test]
    public void GetMissingTypeNames_ShouldReturnOutroOnly_WhenIntroPresent()
    {
        var missing = IntroOutroSegmentCompleteness.GetMissingTypeNames([MediaSegmentType.Intro]);

        missing.Should().ContainSingle()
            .Which.Should().Be(IntroOutroSegmentCompleteness.OutroTypeName);
    }

    [Test]
    public void IsIncomplete_ShouldBeFalse_WhenBothPresent()
    {
        IntroOutroSegmentCompleteness
            .IsIncomplete([MediaSegmentType.Intro, MediaSegmentType.Outro])
            .Should().BeFalse();
    }

    [Test]
    public void IsIncomplete_ShouldBeTrue_WhenOnlyOutroPresent()
    {
        // Incremental detection must still target outro-only episodes for Intro retry.
        IntroOutroSegmentCompleteness
            .IsIncomplete([MediaSegmentType.Outro])
            .Should().BeTrue();
    }

    [Test]
    public void CountsAsPresent_ShouldBeFalse_WhenIntroIsAFewSeconds()
    {
        // A 4s fragment kept when the real intro was rejected.
        IntroOutroSegmentCompleteness
            .CountsAsPresent(MediaSegmentType.Intro, 132_900, 137_400)
            .Should().BeFalse();
    }

    [Test]
    public void CountsAsPresent_ShouldBeTrue_WhenIntroIsLongEnough()
    {
        IntroOutroSegmentCompleteness
            .CountsAsPresent(MediaSegmentType.Intro, 25_800, 140_500)
            .Should().BeTrue();
    }

    [Test]
    public void CountsAsPresent_ShouldBeTrue_WhenSegmentIsAShortOutro()
    {
        IntroOutroSegmentCompleteness
            .CountsAsPresent(MediaSegmentType.Outro, 1_000_000, 1_005_000)
            .Should().BeTrue();
    }

    [Test]
    public void NeedsType_ShouldMatchMissingTypes()
    {
        IReadOnlyCollection<MediaSegmentType> outroOnly = [MediaSegmentType.Outro];

        IntroOutroSegmentCompleteness.NeedsType(outroOnly, MediaSegmentType.Intro).Should().BeTrue();
        IntroOutroSegmentCompleteness.NeedsType(outroOnly, MediaSegmentType.Outro).Should().BeFalse();
    }
}
