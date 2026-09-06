using K7.Server.Application.Common.Interfaces;
using K7.Shared.Security;

namespace K7.Server.Application.Common.Validation;

public static class PasswordValidationRules
{
    public static IRuleBuilderOptions<T, string> MustSatisfyPasswordPolicy<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        IPasswordPolicyService passwordPolicy) =>
        ruleBuilder
            .NotEmpty()
            .MustAsync(async (password, cancellationToken) =>
                PasswordPolicy.IsSatisfiedBy(password, await passwordPolicy.GetAsync(cancellationToken)))
            .WithMessage(_ => PasswordPolicy.Describe(passwordPolicy.Current));
}
