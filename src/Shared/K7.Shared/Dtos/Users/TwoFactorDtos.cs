namespace K7.Shared.Dtos.Users;

public sealed record TwoFactorStatusDto
{
    public required bool IsEnabled { get; init; }
    public required bool HasAuthenticator { get; init; }
    public required int RecoveryCodesLeft { get; init; }
}

public sealed record TwoFactorSetupDto
{
    public required string SharedKey { get; init; }
    public required string AuthenticatorUri { get; init; }
}

public sealed record VerifyTwoFactorRequest
{
    public required string Code { get; init; }
}

public sealed record RecoveryCodesDto
{
    public required IReadOnlyList<string> RecoveryCodes { get; init; }
}
