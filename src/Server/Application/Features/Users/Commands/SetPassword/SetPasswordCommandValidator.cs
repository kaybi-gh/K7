using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Validation;

namespace K7.Server.Application.Features.Users.Commands.SetPassword;

public class SetPasswordCommandValidator : AbstractValidator<SetPasswordCommand>
{
    public SetPasswordCommandValidator(IPasswordPolicyService passwordPolicy)
    {
        RuleFor(v => v.NewPassword).MustSatisfyPasswordPolicy(passwordPolicy);
    }
}
