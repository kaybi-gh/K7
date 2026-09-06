using K7.Shared.Dtos;

namespace K7.Server.Application.Features.PasswordPolicySettings.Commands.UpdatePasswordPolicy;

public class UpdatePasswordPolicyCommandValidator : AbstractValidator<UpdatePasswordPolicyCommand>
{
    public UpdatePasswordPolicyCommandValidator()
    {
        RuleFor(x => x.Policy).NotNull();

        RuleFor(x => x.Policy.RequiredLength)
            .InclusiveBetween(PasswordPolicyDto.MinRequiredLength, PasswordPolicyDto.MaxRequiredLength);

        RuleFor(x => x.Policy.RequiredUniqueChars)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(x => x.Policy.RequiredLength);
    }
}
