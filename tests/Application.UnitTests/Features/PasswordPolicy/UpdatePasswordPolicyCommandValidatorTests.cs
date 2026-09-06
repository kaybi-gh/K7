using K7.Server.Application.Features.PasswordPolicySettings.Commands.UpdatePasswordPolicy;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Features.PasswordPolicySettings;

[TestFixture]
public class UpdatePasswordPolicyCommandValidatorTests
{
    [Test]
    public void Validate_ShouldAcceptDefaults()
    {
        var validator = new UpdatePasswordPolicyCommandValidator();

        validator.Validate(new UpdatePasswordPolicyCommand { Policy = PasswordPolicyDto.Defaults })
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldRejectUniqueCharsAboveLength()
    {
        var validator = new UpdatePasswordPolicyCommandValidator();

        validator.Validate(new UpdatePasswordPolicyCommand
        {
            Policy = new PasswordPolicyDto
            {
                RequiredLength = 8,
                RequiredUniqueChars = 12
            }
        }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldRejectLengthOutOfRange()
    {
        var validator = new UpdatePasswordPolicyCommandValidator();

        validator.Validate(new UpdatePasswordPolicyCommand
        {
            Policy = new PasswordPolicyDto { RequiredLength = 0 }
        }).IsValid.Should().BeFalse();

        validator.Validate(new UpdatePasswordPolicyCommand
        {
            Policy = new PasswordPolicyDto { RequiredLength = PasswordPolicyDto.MaxRequiredLength + 1 }
        }).IsValid.Should().BeFalse();
    }
}
