namespace K7.Shared.Dtos.Users;

public sealed record LoginMethodsDto
{
    public required bool HasPassword { get; init; }
    public required bool CanRemovePassword { get; init; }
    public required bool TwoFactorEnabled { get; init; }
    public required int RecoveryCodesLeft { get; init; }
    public required IReadOnlyList<ExternalLoginDto> ExternalLogins { get; init; }
    public bool CanLinkOidc { get; init; }
    public string? OidcDisplayName { get; init; }
    public bool LocalSignInEnabled { get; init; } = true;
}

public sealed record ExternalLoginDto
{
    public required string Provider { get; init; }
    public string? ProviderDisplayName { get; init; }
    public required bool CanUnlink { get; init; }
}
