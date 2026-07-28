using K7.Shared.Security;

namespace K7.Server.Application.Common.Validation;

public static class PasswordValidationRules
{
    public const string DefaultMessage =
        "Password must be at least 10 characters and include an uppercase letter, a lowercase letter, a digit, and at least 4 unique characters.";

    public static IRuleBuilderOptions<T, string> MustSatisfyPasswordPolicy<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty()
            .Must(PasswordPolicy.IsSatisfiedBy)
            .WithMessage(DefaultMessage);
}
