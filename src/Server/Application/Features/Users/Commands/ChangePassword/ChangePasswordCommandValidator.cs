using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Validation;

namespace K7.Server.Application.Features.Users.Commands.ChangePassword;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator(IPasswordPolicyService passwordPolicy)
    {
        RuleFor(v => v.CurrentPassword).NotEmpty();
        RuleFor(v => v.NewPassword).MustSatisfyPasswordPolicy(passwordPolicy);
    }
}
