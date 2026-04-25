namespace K7.Shared.Dtos.Users;

public sealed record UserMediaExclusionDto
{
    public required Guid MediaId { get; init; }
    public required bool IsAdminExcluded { get; init; }
    public required bool IsSelfExcluded { get; init; }
}
