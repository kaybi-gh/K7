using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record PeerRequestDto
{
    public required Guid Id { get; init; }
    public required string RequesterUrl { get; init; }
    public required string RequesterName { get; init; }
    public required PeerRequestStatus Status { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset? RespondedAt { get; init; }
}
