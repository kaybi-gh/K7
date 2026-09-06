using System.Security.Cryptography;
using K7.Shared.Dtos;

namespace K7.Shared.Security;

/// <summary>
/// Password complexity helpers. Defaults stay aligned with a fresh server; the live
/// policy is stored in server settings and applied to ASP.NET Identity at runtime.
/// </summary>
public static class PasswordPolicy
{
    public const int RequiredLength = PasswordPolicyDto.DefaultRequiredLength;
    public const int RequiredUniqueChars = PasswordPolicyDto.DefaultRequiredUniqueChars;
    public const bool RequireDigit = true;
    public const bool RequireLowercase = true;
    public const bool RequireUppercase = true;
    public const bool RequireNonAlphanumeric = false;

    public static PasswordPolicyDto Normalize(PasswordPolicyDto policy)
    {
        var length = Math.Clamp(policy.RequiredLength, PasswordPolicyDto.MinRequiredLength, PasswordPolicyDto.MaxRequiredLength);
        var unique = Math.Clamp(policy.RequiredUniqueChars, 0, length);

        return policy with
        {
            RequiredLength = length,
            RequiredUniqueChars = unique
        };
    }

    public static bool IsSatisfiedBy(string? password) =>
        IsSatisfiedBy(password, PasswordPolicyDto.Defaults);

    public static bool IsSatisfiedBy(string? password, PasswordPolicyDto policy)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        foreach (var (_, isMet) in Evaluate(password, policy))
        {
            if (!isMet)
                return false;
        }

        return true;
    }

    public static IReadOnlyList<(PasswordRule Rule, bool IsMet)> Evaluate(string? password) =>
        Evaluate(password, PasswordPolicyDto.Defaults);

    public static IReadOnlyList<(PasswordRule Rule, bool IsMet)> Evaluate(string? password, PasswordPolicyDto policy)
    {
        password ??= string.Empty;
        policy = Normalize(policy);

        var rules = new List<(PasswordRule, bool)>
        {
            (PasswordRule.MinLength, password.Length >= policy.RequiredLength)
        };

        if (policy.RequireDigit)
            rules.Add((PasswordRule.Digit, password.Any(char.IsDigit)));

        if (policy.RequireLowercase)
            rules.Add((PasswordRule.Lowercase, password.Any(char.IsLower)));

        if (policy.RequireUppercase)
            rules.Add((PasswordRule.Uppercase, password.Any(char.IsUpper)));

        if (policy.RequireNonAlphanumeric)
            rules.Add((PasswordRule.NonAlphanumeric, password.Any(c => !char.IsLetterOrDigit(c))));

        if (policy.RequiredUniqueChars > 1)
            rules.Add((PasswordRule.UniqueChars, password.Distinct().Count() >= policy.RequiredUniqueChars));

        return rules;
    }

    public static string Describe(PasswordPolicyDto? policy = null)
    {
        policy = Normalize(policy ?? PasswordPolicyDto.Defaults);

        var parts = new List<string> { $"at least {policy.RequiredLength} characters" };

        if (policy.RequireUppercase)
            parts.Add("an uppercase letter");

        if (policy.RequireLowercase)
            parts.Add("a lowercase letter");

        if (policy.RequireDigit)
            parts.Add("a digit");

        if (policy.RequireNonAlphanumeric)
            parts.Add("a special character");

        if (policy.RequiredUniqueChars > 1)
            parts.Add($"at least {policy.RequiredUniqueChars} unique characters");

        if (parts.Count == 1)
            return $"Password must be {parts[0]}.";

        if (parts.Count == 2)
            return $"Password must be {parts[0]} and include {parts[1]}.";

        return $"Password must be {parts[0]} and include {string.Join(", ", parts.Skip(1).Take(parts.Count - 2))}, and {parts[^1]}.";
    }

    public static string Generate(PasswordPolicyDto? policy = null)
    {
        policy = Normalize(policy ?? PasswordPolicyDto.Defaults);

        const string lowers = "abcdefghijkmnopqrstuvwxyz";
        const string uppers = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string specials = "!@#$%^&*-_";

        var chars = new List<char>();

        if (policy.RequireLowercase)
            chars.Add(Pick(lowers));

        if (policy.RequireUppercase)
            chars.Add(Pick(uppers));

        if (policy.RequireDigit)
            chars.Add(Pick(digits));

        if (policy.RequireNonAlphanumeric)
            chars.Add(Pick(specials));

        var alphabet = lowers + uppers + digits;
        if (policy.RequireNonAlphanumeric)
            alphabet += specials;

        var targetLength = Math.Max(policy.RequiredLength, chars.Count);

        while (chars.Count < targetLength || chars.Distinct().Count() < policy.RequiredUniqueChars)
        {
            chars.Add(Pick(alphabet));
            if (chars.Count > PasswordPolicyDto.MaxRequiredLength)
                break;
        }

        Shuffle(chars);
        return new string([.. chars]);
    }

    private static char Pick(string alphabet) =>
        alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

    private static void Shuffle(List<char> chars)
    {
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
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
