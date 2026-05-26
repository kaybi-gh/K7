namespace K7.Shared.Dtos.Users;

public sealed record LoginMethodsDto
{
    public required bool HasPassword { get; init; }
    public required bool CanRemovePassword { get; init; }
    public required IReadOnlyList<ExternalLoginDto> ExternalLogins { get; init; }
}

public sealed record ExternalLoginDto
{
    public required string Provider { get; init; }
    public string? ProviderDisplayName { get; init; }
    public required bool CanUnlink { get; init; }
}
