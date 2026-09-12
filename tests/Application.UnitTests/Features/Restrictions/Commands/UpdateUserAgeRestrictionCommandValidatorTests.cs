using K7.Server.Application.Features.Restrictions.Commands.UpdateUserAgeRestriction;

namespace K7.Server.Application.UnitTests.Features.Restrictions.Commands;

public class UpdateUserAgeRestrictionCommandValidatorTests
{
    private readonly UpdateUserAgeRestrictionCommandValidator _validator = new();

    [Test]
    public void Validate_ShouldFail_WhenEnabledWithoutDateOfBirth()
    {
        var result = _validator.Validate(new UpdateUserAgeRestrictionCommand
        {
            UserId = Guid.NewGuid(),
            Enabled = true,
            DateOfBirth = null
        });

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldFail_WhenDateOfBirthIsInTheFuture()
    {
        var result = _validator.Validate(new UpdateUserAgeRestrictionCommand
        {
            UserId = Guid.NewGuid(),
            Enabled = false,
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        });

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldPass_WhenDisabledWithoutDateOfBirth()
    {
        var result = _validator.Validate(new UpdateUserAgeRestrictionCommand
        {
            UserId = Guid.NewGuid(),
            Enabled = false,
            DateOfBirth = null
        });

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldPass_WhenEnabledWithPastDateOfBirth()
    {
        var result = _validator.Validate(new UpdateUserAgeRestrictionCommand
        {
            UserId = Guid.NewGuid(),
            Enabled = true,
            DateOfBirth = new DateOnly(2014, 9, 10)
        });

        result.IsValid.Should().BeTrue();
    }
}
