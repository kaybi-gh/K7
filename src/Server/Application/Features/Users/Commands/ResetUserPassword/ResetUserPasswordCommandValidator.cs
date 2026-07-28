using K7.Server.Application.Common.Validation;

namespace K7.Server.Application.Features.Users.Commands.ResetUserPassword;

public class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.NewPassword).MustSatisfyPasswordPolicy().MaximumLength(200);
    }
}
