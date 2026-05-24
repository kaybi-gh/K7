using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record LibraryStatisticsDto
{
    public required Guid LibraryId { get; init; }
    public required int FileCount { get; init; }
    public required Dictionary<MediaType, int> MediaCounts { get; init; }
}
