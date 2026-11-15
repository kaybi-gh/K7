using K7.Server.Application.Features.Home.Commands.UpdateUserHomeLayout;
using K7.Shared.Dtos.Home;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.Home.Commands;

[TestFixture]
public class UpdateUserHomeLayoutCommandValidatorTests
{
    private static HomeRowConfigDto Row(HomeRowDisplayType displayType = HomeRowDisplayType.Carousel, int order = 0) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Row",
        DisplayType = displayType,
        PageSize = 20,
        ContinueWatching = false,
        IsVisible = true,
        Order = order
    };

    private static bool IsValid(params HomeRowConfigDto[] rows) =>
        new UpdateUserHomeLayoutCommandValidator()
            .Validate(new UpdateUserHomeLayoutCommand { Layout = new HomeLayoutDto { Rows = rows } })
            .IsValid;

    [TestCase(HomeRowDisplayType.Carousel)]
    [TestCase(HomeRowDisplayType.Landscape)]
    [TestCase(HomeRowDisplayType.Featured)]
    [TestCase(HomeRowDisplayType.FeaturedBleed)]
    public void Validate_ShouldPass_WhenDisplayTypeIsKnown(HomeRowDisplayType displayType)
    {
        IsValid(Row(displayType)).Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldFail_WhenDisplayTypeIsUnknown()
    {
        IsValid(Row((HomeRowDisplayType)99)).Should().BeFalse();
    }

    [TestCase(null, true)]
    [TestCase(HomeLayoutDto.MinAutoAdvanceSeconds, true)]
    [TestCase(HomeLayoutDto.MaxAutoAdvanceSeconds, true)]
    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(HomeLayoutDto.MaxAutoAdvanceSeconds + 1, false)]
    public void Validate_ShouldBoundTheAutoAdvanceDelay(int? seconds, bool expected)
    {
        IsValid(Row(HomeRowDisplayType.Featured) with { AutoAdvanceSeconds = seconds }).Should().Be(expected);
    }

    [Test]
    public void Validate_ShouldPass_WhenTheOnlyBannerIsTheFirstRow()
    {
        IsValid(Row(HomeRowDisplayType.FeaturedBleed, order: 0), Row(order: 1)).Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldFail_WhenTheBannerIsNotFirstOrThereAreTwo()
    {
        IsValid(Row(order: 0), Row(HomeRowDisplayType.Featured, order: 1)).Should().BeFalse();
        IsValid(Row(HomeRowDisplayType.Featured, order: 0), Row(HomeRowDisplayType.FeaturedBleed, order: 1))
            .Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldPass_WhenLayoutHoldsTheMostRows()
    {
        var rows = Enumerable.Range(0, HomeLayoutDto.MaxRows).Select(i => Row(order: i)).ToArray();

        IsValid(rows).Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldFail_WhenLayoutHoldsTooManyRows()
    {
        var rows = Enumerable.Range(0, HomeLayoutDto.MaxRows + 1).Select(i => Row(order: i)).ToArray();

        IsValid(rows).Should().BeFalse();
    }
}
