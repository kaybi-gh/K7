using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileAgeRestriction;

namespace K7.Server.Application.UnitTests.Features.SharedProfiles;

public class UpdateSharedProfileAgeRestrictionCommandValidatorTests
{
    private readonly UpdateSharedProfileAgeRestrictionCommandValidator _validator = new();

    [Test]
    public void Validate_ShouldFail_WhenEnabledWithoutDateOfBirth()
    {
        var result = _validator.Validate(new UpdateSharedProfileAgeRestrictionCommand
        {
            SharedProfileId = Guid.NewGuid(),
            Enabled = true,
            DateOfBirth = null
        });

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldPass_WhenEnabledWithPastDateOfBirth()
    {
        var result = _validator.Validate(new UpdateSharedProfileAgeRestrictionCommand
        {
            SharedProfileId = Guid.NewGuid(),
            Enabled = true,
            DateOfBirth = new DateOnly(2014, 9, 10),
            HideUnratedTitles = false
        });

        result.IsValid.Should().BeTrue();
    }
}
