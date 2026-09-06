using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Validation;

namespace K7.Server.Application.Features.Users.Commands.ResetUserPassword;

public class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator(IPasswordPolicyService passwordPolicy)
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.NewPassword).MustSatisfyPasswordPolicy(passwordPolicy).MaximumLength(200);
    }
}
