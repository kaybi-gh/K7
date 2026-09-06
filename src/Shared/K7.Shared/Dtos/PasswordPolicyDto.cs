namespace K7.Shared.Dtos;

public sealed record PasswordPolicyDto
{
    public const int DefaultRequiredLength = 10;
    public const int DefaultRequiredUniqueChars = 4;
    public const int MinRequiredLength = 1;
    public const int MaxRequiredLength = 128;

    public int RequiredLength { get; init; } = DefaultRequiredLength;
    public int RequiredUniqueChars { get; init; } = DefaultRequiredUniqueChars;
    public bool RequireDigit { get; init; } = true;
    public bool RequireLowercase { get; init; } = true;
    public bool RequireUppercase { get; init; } = true;
    public bool RequireNonAlphanumeric { get; init; } = false;

    public static PasswordPolicyDto Defaults { get; } = new();
}
