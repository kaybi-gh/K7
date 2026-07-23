namespace K7.Server.Application.Features.Users.Commands.VerifyTwoFactorSetup;

public class VerifyTwoFactorSetupCommandValidator : AbstractValidator<VerifyTwoFactorSetupCommand>
{
    public VerifyTwoFactorSetupCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    }
}
