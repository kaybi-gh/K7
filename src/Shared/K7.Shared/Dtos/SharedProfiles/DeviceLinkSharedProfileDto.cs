namespace K7.Shared.Dtos.SharedProfiles;

public sealed record DeviceLinkSharedProfileDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required bool HasPin { get; init; }
    public string? AvatarUrl { get; init; }
}
