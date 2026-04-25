namespace K7.Shared.Dtos.Users;

public sealed record UserLibraryExclusionDto
{
    public required Guid LibraryId { get; init; }
    public required bool IsAdminExcluded { get; init; }
    public required bool IsSelfExcluded { get; init; }
}
