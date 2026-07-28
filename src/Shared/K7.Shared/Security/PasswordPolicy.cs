namespace K7.Shared.Security;

/// <summary>
/// Password complexity rules. Must stay aligned with ASP.NET Identity options in
/// Infrastructure Database Context DI.
/// </summary>
public static class PasswordPolicy
{
    public const int RequiredLength = 10;
    public const int RequiredUniqueChars = 4;
    public const bool RequireDigit = true;
    public const bool RequireLowercase = true;
    public const bool RequireUppercase = true;
    public const bool RequireNonAlphanumeric = false;

    public static bool IsSatisfiedBy(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        foreach (var (_, isMet) in Evaluate(password))
        {
            if (!isMet)
                return false;
        }

        return true;
    }

    public static IReadOnlyList<(PasswordRule Rule, bool IsMet)> Evaluate(string? password)
    {
        password ??= string.Empty;

        // Keep in sync with the Require* constants above (NonAlphanumeric is currently off).
        return
        [
            (PasswordRule.MinLength, password.Length >= RequiredLength),
            (PasswordRule.Digit, password.Any(char.IsDigit)),
            (PasswordRule.Lowercase, password.Any(char.IsLower)),
            (PasswordRule.Uppercase, password.Any(char.IsUpper)),
            (PasswordRule.UniqueChars, password.Distinct().Count() >= RequiredUniqueChars)
        ];
    }
}

public enum PasswordRule
{
    MinLength,
    Digit,
    Lowercase,
    Uppercase,
    NonAlphanumeric,
    UniqueChars
}
